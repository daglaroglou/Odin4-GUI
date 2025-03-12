using Gtk;
using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static TextView logTextView = new TextView { Editable = false, Monospace = true };

    static void Main()
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
        paned.Position = 900; // Adjust the initial position to make the logs tab wider
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

        // Test print to log
        AppendLog("Odin4 started.");

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
            logTextView.Buffer.Text += "  " + message + "\n"; // Add padding before the text
            logTextView.ScrollToIter(logTextView.Buffer.EndIter, 0, false, 0, 0);
        });
    }
}
