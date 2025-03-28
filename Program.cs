using Gtk;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace Aesir
{
    public class Program
    {
        // UI Elements
        private static Window? mainWindow;
        private static TextView? logTextView;

        public static void Main(string[] args)
        {
            try
            {
                // Initialize cancellation token source
                Helper.CancellationTokenSource = new CancellationTokenSource();

                // Initialize GTK
                Application.Init();

                // Store the UI synchronization context
                Helper.UiSynchronizationContext = SynchronizationContext.Current;

                // Create and start the UI thread
                Thread uiThread = new Thread(() => RunUI());
                uiThread.IsBackground = false; // Make this a foreground thread
                uiThread.Start();

                // Wait for the UI to be initialized
                Helper.UiInitializedEvent.WaitOne();

                // Start the background tasks on a separate thread
                Thread backgroundThread = new Thread(async () => await Helper.RunBackgroundTasks());
                backgroundThread.IsBackground = true;
                backgroundThread.Start();

                // Keep the main thread alive until the application exits
                Application.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error in Main: {ex}");
                Helper.DisplayErrorMessage($"Fatal error: {ex.Message}\n\nPlease report this issue.");
            }
        }

        private static void RunUI()
        {
            try
            {
                // Set application-wide dark theme preference
                Settings.Default.ApplicationPreferDarkTheme = true;

                // Create main window
                mainWindow = new Window("Aesir");
                mainWindow.SetDefaultSize(1200, 700);
                mainWindow.DeleteEvent += OnWindowClosed;

                // Create notebook (tab container)
                Notebook notebook = new Notebook();

                // Create tabs
                notebook.AppendPage(CreateOdinTab(), new Label("Odin"));
                notebook.AppendPage(CreateAdbTab(), new Label("ADB"));
                notebook.AppendPage(CreateGappsTab(), new Label("GAPPS"));

                // Create a vertical box to hold the notebook and the footer
                Box mainBox = new Box(Orientation.Vertical, 0);
                mainBox.PackStart(notebook, true, true, 0);

                // Create footer label with clickable link and version information
                Label footerLabel = new Label();
                footerLabel.Markup = "Made by <a href=\"https://github.com/daglaroglou\">daglaroglou</a> with ❤️ · <span size='small'>v1.0</span>";
                footerLabel.Selectable = false;
                mainBox.PackEnd(footerLabel, false, false, 10);

                // Add main box to window
                mainWindow.Add(mainBox);
                mainWindow.ShowAll();

                // Signal that UI is initialized
                Helper.UiInitializedEvent.Set();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error initializing UI: {ex}");
                Helper.DisplayErrorMessage($"Error initializing UI: {ex.Message}");
                Helper.UiInitializedEvent.Set(); // Signal even on error
            }
        }

        private static Widget CreateOdinTab()
        {
            // Create a horizontally-oriented paned container
            Paned paned = new Paned(Orientation.Horizontal);
            paned.Position = 880;

            // Left side: Odin options
            Box odinBox = new Box(Orientation.Vertical, 2);

            // Create a horizontal container to place the grid and the image side by side
            Box contentBox = new Box(Orientation.Horizontal, 10);
            odinBox.PackStart(contentBox, false, false, 10); // Adjusted padding here

            // Create the grid for firmware options
            Grid grid = CreateFirmwareOptionsGrid();
            contentBox.PackStart(grid, false, false, 0);

            // Add Samsung image next to the boxes
            var samsungImage = Helper.LoadEmbeddedImage("Aesir.Resources.samsung.png", 256, 85);
            if (samsungImage != null)
            {
                Image imageWidget = new Image(samsungImage);
                contentBox.PackStart(imageWidget, false, false, 120); // Adjusted padding here
            }
            else
            {
                Label errorLabel = new Label("Failed to load image.");
                contentBox.PackStart(errorLabel, false, false, 120); // Adjusted padding here
            }

            // Add instructions for entering Download Mode
            Label downloadModeInstructions = new Label
            {
                Markup = "<b>How to Enter Download Mode:</b>\n1. Power off your device.\n2. Press and hold Volume Down + Home + Power buttons simultaneously.\n3. When prompted, press Volume Up to confirm.",
                LineWrap = true,
                Justify = Justification.Left
            };
            odinBox.PackStart(downloadModeInstructions, true, true, 10);

            // Right side: Logs
            Box rightBox = new Box(Orientation.Vertical, 2);
            paned.Pack2(rightBox, true, false);

            // Add logs title
            Label logTitleLabel = new Label("Logs");
            logTitleLabel.Selectable = false;
            rightBox.PackStart(logTitleLabel, false, false, 0);

            // Create log view with scrolling
            logTextView = new TextView { Editable = false, Monospace = true };

            // Pass the log text view to the Helper class
            if (logTextView != null)
            {
                Helper.SetLogTextView(logTextView);
            }

            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.Add(logTextView);
            rightBox.PackStart(scrolledWindow, true, true, 0);

            // Add Save Logs button
            Button saveLogsButton = new Button("Save Logs");
            saveLogsButton.Clicked += (sender, e) => SaveLogsToFile();
            rightBox.PackStart(saveLogsButton, false, false, 10);

            paned.Pack1(odinBox, true, false);

            return paned;
        }

        private static Grid CreateFirmwareOptionsGrid()
        {
            string[] options = { "BL", "AP", "CP", "CSC", "USERDATA" };
            Grid grid = new Grid { ColumnSpacing = 5, RowSpacing = 5 };

            // Create rows for each firmware option
            for (int i = 0; i < options.Length; i++)
            {
                CheckButton checkButton = new CheckButton(options[i]);
                Entry entry = new Entry();
                Button browseButton = new Button("Browse");

                int index = i; // Capture the current index
                browseButton.Clicked += (sender, e) => Helper.BrowseForFile(entry, mainWindow);

                grid.Attach(checkButton, 0, i, 1, 1);
                grid.Attach(entry, 1, i, 1, 1);
                grid.Attach(browseButton, 2, i, 1, 1);
            }

            // Add Start and Reset buttons
            Button startButton = new Button("Start");
            Button resetButton = new Button("Reset");

            startButton.Clicked += async (sender, e) => await Helper.StartOdinProcess(grid, options);
            resetButton.Clicked += (sender, e) => Helper.ResetForm(grid);

            // Attach buttons to the grid
            grid.Attach(startButton, 0, options.Length, 2, 1);
            grid.Attach(resetButton, 2, options.Length, 1, 1);

            return grid;
        }

        private static Widget CreateAdbTab()
        {
            Box adbBox = new Box(Orientation.Vertical, 2);
            var image = Helper.LoadEmbeddedImage("Aesir.Resources.construction.png", 200, 220);

            if (image != null)
            {
                Image imageWidget = new Image(image);
                adbBox.PackStart(imageWidget, true, true, 0);
            }
            else
            {
                Label errorLabel = new Label("Failed to load image.");
                adbBox.PackStart(errorLabel, true, true, 0);
            }

            return adbBox;
        }

        private static Widget CreateGappsTab()
        {
            Box gappsBox = new Box(Orientation.Vertical, 2);
            var image = Helper.LoadEmbeddedImage("Aesir.Resources.construction.png", 200, 220);

            if (image != null)
            {
                Image imageWidget = new Image(image);
                gappsBox.PackStart(imageWidget, true, true, 0);
            }
            else
            {
                Label errorLabel = new Label("Failed to load image.");
                gappsBox.PackStart(errorLabel, true, true, 0);
            }
            return gappsBox;
        }

        private static void OnWindowClosed(object? sender, DeleteEventArgs args)
        {
            // Clean up resources when the window is closed
            Helper.CleanupResources();
            Application.Quit();
        }

        private static void SaveLogsToFile()
        {
            try
            {
                // Open a file chooser dialog to select the save location
                FileChooserDialog fileChooser = new FileChooserDialog(
                    "Save Logs",
                    mainWindow,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept
                );

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    string filePath = fileChooser.Filename;
                    fileChooser.Destroy();

                    // Get the logs from the TextView
                    string logs = logTextView?.Buffer?.Text ?? string.Empty;

                    // Write the logs to the selected file
                    System.IO.File.WriteAllText(filePath, logs);

                    // Display a success message
                    Helper.AppendLog("Logs saved successfully.");
                }
                else
                {
                    fileChooser.Destroy();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving logs: {ex}");
                Helper.DisplayErrorMessage($"Error saving logs: {ex.Message}");
            }
        }
    }
}
