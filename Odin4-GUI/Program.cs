using FindADBTools;
using Gtk;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Timers;

namespace Odin4GUI
{
    class Program
    {
        static TextView logTextView = new TextView { Editable = false, Monospace = true };
        static System.Timers.Timer deviceCheckTimer = new System.Timers.Timer();
        static bool isConnected = false; // Guard variable to track connection status
        static string connectedDeviceId = string.Empty; // Variable to store the ID of the connected device

        static async Task Main(string[] args)
        {
            if (!IsRunningAsRoot())
            {
                ShowSudoRequiredMessage();
                return;
            }

            Application.Init();

            Settings.Default.ApplicationPreferDarkTheme = true;

            Window window = new Window("Odin4");
            window.SetDefaultSize(1200, 700);
            window.DeleteEvent += (o, args) => Application.Quit();

            Notebook notebook = new Notebook();

            // Odin Tab
            Box odinBox = new Box(Orientation.Vertical, 2);

            string[] options = { "BL", "AP", "CP", "CSC", "USERDATA" };
            Grid grid = new Grid();
            grid.ColumnSpacing = 5;
            grid.RowSpacing = 5;

            for (int i = 0; i < options.Length; i++)
            {
                CheckButton checkButton = new CheckButton(options[i]);
                Entry entry = new Entry();
                Button browseButton = new Button("Browse");

                browseButton.Clicked += (sender, e) =>
                {
                    FileChooserDialog fileChooser = new FileChooserDialog(
                        "Choose the file",
                        null,
                        FileChooserAction.Open,
                        "Cancel", ResponseType.Cancel,
                        "Open", ResponseType.Accept);

                    if (fileChooser.Run() == (int)ResponseType.Accept)
                    {
                        entry.Text = fileChooser.Filename;
                        AppendLog($"Selected file: {fileChooser.Filename}");
                    }

                    fileChooser.Destroy();
                };

                grid.Attach(checkButton, 0, i, 1, 1);
                grid.Attach(entry, 1, i, 1, 1);
                grid.Attach(browseButton, 2, i, 1, 1);
            }

            odinBox.PackStart(grid, false, false, 0);

            // Create a Paned widget to split the view
            Paned paned = new Paned(Orientation.Horizontal);
            paned.Position = 880; // Adjust the initial position to make the logs tab wider
            paned.Pack1(odinBox, true, false);

            // Create a VBox for the right side
            Box rightBox = new Box(Orientation.Vertical, 2);

            // Add a title label to the right side
            Label logTitleLabel = new Label("Logs");
            rightBox.PackStart(logTitleLabel, false, false, 0);

            // Create a ScrolledWindow to make the TextView scrollable
            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic); // Ensure scrollbars are shown automatically
            scrolledWindow.Add(logTextView);

            // Pack the ScrolledWindow into the rightBox
            rightBox.PackStart(scrolledWindow, true, true, 0);

            // Pack the rightBox into the right side of the Paned widget
            paned.Pack2(rightBox, true, false);

            // Add the Paned widget to the notebook
            notebook.AppendPage(paned, new Label("Odin"));

            // ADB Tab
            Box adbBox = new Box(Orientation.Vertical, 2);
            Label adbLabel = new Label("SOON");
            adbBox.PackStart(adbLabel, true, true, 0);
            notebook.AppendPage(adbBox, new Label("ADB"));

            // GAPPS Tab
            Box gappsBox = new Box(Orientation.Vertical, 2);
            Label gappsLabel = new Label("SOON");
            gappsBox.PackStart(gappsLabel, true, true, 0);
            notebook.AppendPage(gappsBox, new Label("GAPPS"));

            window.Add(notebook);
            window.ShowAll();

            // Run the background tasks after the UI is shown
            _ = Task.Run(async () =>
            {
                // Wait for 1 second before checking for Odin and ADB
                await Task.Delay(1000);

                while (!await FindAdbTools.ADBFound() || !await FindAdbTools.FastbootFound())
                {
                    AppendLog("ADB tools not found.");
                }

                string adbVersion = await GetCommandOutput("sh", "-c \"adb version | sed -n '2p' | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                AppendLog($"ADB tools found. (v{adbVersion})");

                while (!await FindOdin.FindOdin.OdinFound())
                {
                    AppendLog("Odin4 not found.");
                }

                string odinVersion = await GetCommandOutput("sh", "-c \"sudo odin4 -v | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                AppendLog($"Odin4 found (v{odinVersion})");

                // Run the sudo odin -l command in the background
                RunOdinCommand();

                // Initialize and start the device check timer
                deviceCheckTimer = new System.Timers.Timer(1000); // Check every 1 second
                deviceCheckTimer.Elapsed += CheckDeviceConnection;
                deviceCheckTimer.Start();
            });

            Application.Run();
        }

        private static bool IsRunningAsRoot()
        {
            return Environment.UserName == "root";
        }

        private static void ShowSudoRequiredMessage()
        {
            Application.Init();
            MessageDialog md = new MessageDialog(null, 
                DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok,
                "This program requires sudo permissions to run. Please restart it with 'sudo'.");
            md.Run();
            md.Destroy();
            Application.Quit();
        }

        private static void AppendLog(string message)
        {
            Application.Invoke(delegate
            {
                TextBuffer buffer = logTextView.Buffer;
                TextIter endIter = buffer.EndIter;
                string timestamp = DateTime.Now.ToString("  [HH:mm] > ");
                buffer.Insert(ref endIter,  timestamp + message + "\n");

                // Create a tag for smaller font size
                TextTag tag = new TextTag(null);
                tag.SizePoints = 10; // Set the font size to 10 points
                buffer.TagTable.Add(tag);

                // Apply the tag to the newly inserted text
                buffer.ApplyTag(tag, buffer.StartIter, buffer.EndIter);

                logTextView.ScrollToIter(buffer.EndIter, 0, false, 0, 0);
            });
        }

        private static void CheckDeviceConnection(object? sender, ElapsedEventArgs e)
        {
            Task.Run(async () =>
            {
                string output = await GetCommandOutput("sudo", "odin4 -l");
                Match match = Regex.Match(output, @"\d{3}$");
                if (match.Success)
                {
                    string id = match.Value;
                    if (!isConnected)
                    {
                        isConnected = true;
                        connectedDeviceId = id;
                        AppendLog($"<ID:{id}> Connected.");
                    }
                }
                else
                {
                    if (isConnected)
                    {
                        isConnected = false;
                        AppendLog($"<ID:{connectedDeviceId}> Disconnected.");
                        connectedDeviceId = string.Empty; // Reset the connected device ID
                    }
                }
            });
        }

        private static void RunOdinCommand()
        {
            Process process = new Process();
            process.StartInfo.FileName = "sudo";
            process.StartInfo.Arguments = "odin4 -l";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Extract the last three numbers from the output
                    Match match = Regex.Match(e.Data, @"\d{3}$");
                    if (match.Success)
                    {
                        string id = match.Value;
                        AppendLog($"<ID:{id}> Connected.");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
        }

        private static async Task<string> GetCommandOutput(string command, string arguments)
        {
            Process process = new Process();
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            return output.Trim();
        }
    }
}