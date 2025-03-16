using Gtk;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Odin4GUI
{
    public class Program
    {
        // UI Elements
        private static Window mainWindow;
        private static TextView logTextView;

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
                Thread uiThread = new Thread(RunUI);
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
                mainWindow = new Window("Odin4 GUI");
                mainWindow.SetDefaultSize(1200, 700);
                mainWindow.DeleteEvent += OnWindowClosed;

                // Create notebook (tab container)
                Notebook notebook = new Notebook();

                // Create tabs
                notebook.AppendPage(CreateOdinTab(), new Label("Odin"));
                notebook.AppendPage(CreateAdbTab(), new Label("ADB"));
                notebook.AppendPage(CreateGappsTab(), new Label("GAPPS"));

                // Add notebook to window
                mainWindow.Add(notebook);
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
            paned.Pack1(odinBox, true, false);

            // Create the grid for firmware options
            Grid grid = CreateFirmwareOptionsGrid();
            odinBox.PackStart(grid, false, false, 0);

            // Right side: Logs
            Box rightBox = new Box(Orientation.Vertical, 2);
            paned.Pack2(rightBox, true, false);

            // Add logs title
            Label logTitleLabel = new Label("Logs");
            rightBox.PackStart(logTitleLabel, false, false, 0);

            // Create log view with scrolling
            logTextView = new TextView { Editable = false, Monospace = true };

            // Pass the log text view to the Helper class
            Helper.SetLogTextView(logTextView);

            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            scrolledWindow.Add(logTextView);
            rightBox.PackStart(scrolledWindow, true, true, 0);

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
            Label adbLabel = new Label("ADB functionality coming soon");
            adbBox.PackStart(adbLabel, true, true, 0);
            return adbBox;
        }

        private static Widget CreateGappsTab()
        {
            Box gappsBox = new Box(Orientation.Vertical, 2);
            Label gappsLabel = new Label("GAPPS functionality coming soon");
            gappsBox.PackStart(gappsLabel, true, true, 0);
            return gappsBox;
        }

        private static void OnWindowClosed(object sender, DeleteEventArgs args)
        {
            // Clean up resources when the window is closed
            Helper.CleanupResources();
            Application.Quit();
        }
    }
}