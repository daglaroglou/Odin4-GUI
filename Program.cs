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
        private static TextView? adbLogTextView;

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
                notebook.AppendPage(CreateOtherTab(), new Label("Other"));

                // Create a vertical box to hold the notebook and the footer
                Box mainBox = new Box(Orientation.Vertical, 0);
                mainBox.PackStart(notebook, true, true, 0);

                // Create footer label with clickable link and version information
                Label footerLabel = new Label();
                footerLabel.Markup = "Made by <a href=\"https://github.com/daglaroglou\">daglaroglou</a> with ❤️ · <span size='small'>v1.1.1</span>";
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
            // Create a horizontally-oriented paned container
            Paned paned = new Paned(Orientation.Horizontal);
            paned.Position = 600;

            // Left side: ADB options
            Box adbBox = new Box(Orientation.Vertical, 10);

            // Device Information Section
            Frame deviceFrame = new Frame();
            Label deviceFrameLabel = new Label
            {
                Markup = "<b>Device Information</b>",
                Xalign = 0.5f
            };
            deviceFrame.LabelWidget = deviceFrameLabel;
            Box deviceBox = new Box(Orientation.Vertical, 5);

            Button refreshDevicesButton = new Button("Refresh Connected Devices") { WidthRequest = 200 };
            refreshDevicesButton.Clicked += async (sender, e) => await RefreshAdbDevices();
            Button deviceInfoButton = new Button("Get Device Info") { WidthRequest = 200 };
            deviceInfoButton.Clicked += async (sender, e) => await GetDeviceInfo();
            Button batteryInfoButton = new Button("Battery Info") { WidthRequest = 200 };
            batteryInfoButton.Clicked += async (sender, e) => await GetBatteryInfo();

            deviceBox.PackStart(refreshDevicesButton, false, false, 0);
            deviceBox.PackStart(deviceInfoButton, false, false, 0);
            deviceBox.PackStart(batteryInfoButton, false, false, 0);
            deviceFrame.Add(deviceBox);
            adbBox.PackStart(deviceFrame, false, false, 0);

            // Package Management Section
            Frame packageFrame = new Frame();
            Label packageFrameLabel = new Label
            {
                Markup = "<b>Package Management</b>",
                Xalign = 0.5f
            };
            packageFrame.LabelWidget = packageFrameLabel;
            Box packageBox = new Box(Orientation.Vertical, 5);

            Button listPackagesButton = new Button("List Installed Packages") { WidthRequest = 200 };
            listPackagesButton.Clicked += async (sender, e) => await ListInstalledPackages();
            
            // Package installation/uninstallation
            Box packageActionBox = new Box(Orientation.Horizontal, 5);
            Entry packageNameEntry = new Entry { PlaceholderText = "Package name or APK path" };
            Button installButton = new Button("Install APK");
            installButton.Clicked += async (sender, e) => await InstallPackage(packageNameEntry.Text);
            Button uninstallButton = new Button("Uninstall");
            uninstallButton.Clicked += async (sender, e) => await UninstallPackage(packageNameEntry.Text);
            Button browseApkButton = new Button("Browse APK");
            browseApkButton.Clicked += (sender, e) => Helper.BrowseForFile(packageNameEntry, mainWindow);

            packageActionBox.PackStart(packageNameEntry, true, true, 0);
            packageActionBox.PackStart(browseApkButton, false, false, 0);
            packageBox.PackStart(packageActionBox, false, false, 0);

            Box installUninstallBox = new Box(Orientation.Horizontal, 5);
            installUninstallBox.PackStart(installButton, true, true, 0);
            installUninstallBox.PackStart(uninstallButton, true, true, 0);
            packageBox.PackStart(installUninstallBox, false, false, 0);

            packageFrame.Add(packageBox);
            adbBox.PackStart(packageFrame, false, false, 0);

            // File Management Section
            Frame fileFrame = new Frame();
            Label fileFrameLabel = new Label
            {
                Markup = "<b>File Management</b>",
                Xalign = 0.5f
            };
            fileFrame.LabelWidget = fileFrameLabel;
            Box fileBox = new Box(Orientation.Vertical, 5);

            // Push file
            Box pushBox = new Box(Orientation.Horizontal, 5);
            Entry localFileEntry = new Entry { PlaceholderText = "Local file path" };
            Entry remotePathEntry = new Entry { PlaceholderText = "Remote path (e.g., /sdcard/)" };
            Button browsePushButton = new Button("Browse");
            browsePushButton.Clicked += (sender, e) => Helper.BrowseForFile(localFileEntry, mainWindow);
            Button pushButton = new Button("Push File");
            pushButton.Clicked += async (sender, e) => await PushFile(localFileEntry.Text, remotePathEntry.Text);

            pushBox.PackStart(localFileEntry, true, true, 0);
            pushBox.PackStart(browsePushButton, false, false, 0);
            fileBox.PackStart(pushBox, false, false, 0);
            fileBox.PackStart(remotePathEntry, false, false, 0);
            fileBox.PackStart(pushButton, false, false, 0);

            // Pull file
            Box pullBox = new Box(Orientation.Horizontal, 5);
            Entry remoteFileEntry = new Entry { PlaceholderText = "Remote file path" };
            Entry localPathEntry = new Entry { PlaceholderText = "Local destination path" };
            Button pullButton = new Button("Pull File");
            pullButton.Clicked += async (sender, e) => await PullFile(remoteFileEntry.Text, localPathEntry.Text);

            pullBox.PackStart(remoteFileEntry, true, true, 0);
            pullBox.PackStart(localPathEntry, true, true, 0);
            fileBox.PackStart(pullBox, false, false, 0);
            fileBox.PackStart(pullButton, false, false, 0);

            fileFrame.Add(fileBox);
            adbBox.PackStart(fileFrame, false, false, 0);

            // System Actions Section
            Frame systemFrame = new Frame();
            Label systemFrameLabel = new Label
            {
                Markup = "<b>System Actions</b>",
                Xalign = 0.5f
            };
            systemFrame.LabelWidget = systemFrameLabel;
            Box systemBox = new Box(Orientation.Vertical, 5);

            Box systemButtonsBox1 = new Box(Orientation.Horizontal, 5);
            Button rebootButton = new Button("Reboot Device") { WidthRequest = 95 };
            rebootButton.Clicked += async (sender, e) => await RebootDevice();
            Button rebootBootloaderButton = new Button("Reboot to Bootloader") { WidthRequest = 95 };
            rebootBootloaderButton.Clicked += async (sender, e) => await RebootToBootloader();

            systemButtonsBox1.PackStart(rebootButton, true, true, 0);
            systemButtonsBox1.PackStart(rebootBootloaderButton, true, true, 0);
            systemBox.PackStart(systemButtonsBox1, false, false, 0);

            Box systemButtonsBox2 = new Box(Orientation.Horizontal, 5);
            Button rebootRecoveryButton = new Button("Reboot to Recovery") { WidthRequest = 95 };
            rebootRecoveryButton.Clicked += async (sender, e) => await RebootToRecovery();
            Button rebootDownloadButton = new Button("Reboot to Download") { WidthRequest = 95 };
            rebootDownloadButton.Clicked += async (sender, e) => await RebootToDownload();

            systemButtonsBox2.PackStart(rebootRecoveryButton, true, true, 0);
            systemButtonsBox2.PackStart(rebootDownloadButton, true, true, 0);
            systemBox.PackStart(systemButtonsBox2, false, false, 0);

            Box systemButtonsBox3 = new Box(Orientation.Horizontal, 5);
            Button logcatButton = new Button("Start Logcat") { WidthRequest = 200 };
            logcatButton.Clicked += async (sender, e) => await StartLogcat();

            systemButtonsBox3.PackStart(logcatButton, true, true, 0);
            systemBox.PackStart(systemButtonsBox3, false, false, 0);

            systemFrame.Add(systemBox);
            adbBox.PackStart(systemFrame, false, false, 0);

            // Shell Section
            Frame shellFrame = new Frame();
            Label shellFrameLabel = new Label
            {
                Markup = "<b>Shell Commands</b>",
                Xalign = 0.5f
            };
            shellFrame.LabelWidget = shellFrameLabel;
            Box shellBox = new Box(Orientation.Vertical, 5);

            Entry shellCommandEntry = new Entry { PlaceholderText = "Enter shell command" };
            Button executeShellButton = new Button("Execute Shell Command");
            executeShellButton.Clicked += async (sender, e) => await ExecuteShellCommand(shellCommandEntry.Text);

            shellBox.PackStart(shellCommandEntry, false, false, 0);
            shellBox.PackStart(executeShellButton, false, false, 0);

            shellFrame.Add(shellBox);
            adbBox.PackStart(shellFrame, false, false, 0);

            // Right side: ADB Logs (create separate log view for ADB)
            Box rightBox = new Box(Orientation.Vertical, 2);
            paned.Pack2(rightBox, true, false);

            Label adbLogTitleLabel = new Label("ADB Output");
            adbLogTitleLabel.Selectable = false;
            rightBox.PackStart(adbLogTitleLabel, false, false, 0);

            // Create separate log view for ADB
            adbLogTextView = new TextView { Editable = false, Monospace = true };
            ScrolledWindow adbScrolledWindow = new ScrolledWindow();
            adbScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
            adbScrolledWindow.Add(adbLogTextView);
            rightBox.PackStart(adbScrolledWindow, true, true, 0);

            // Add Save ADB Logs button
            Button saveAdbLogsButton = new Button("Save ADB Logs");
            saveAdbLogsButton.Clicked += (sender, e) => SaveAdbLogsToFile();
            rightBox.PackStart(saveAdbLogsButton, false, false, 10);

            paned.Pack1(adbBox, true, false);

            return paned;
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

        private static Widget CreateOtherTab()
        {
            // Create a vertical box to hold all sections
            Box otherBox = new Box(Orientation.Vertical, 10);

            // Section 1: Firmware Links
            Frame firmwareFrame = new Frame();
            Label firmwareFrameLabel = new Label
            {
                Markup = "<b>Firmware Links</b>",
                Xalign = 0.5f // Center align the label horizontally
            };
            firmwareFrame.LabelWidget = firmwareFrameLabel;
            Box firmwareBox = new Box(Orientation.Vertical, 5);

            Button samMobileButton = new Button("SamMobile") { WidthRequest = 150 };
            samMobileButton.Clicked += (sender, e) => OpenUrl("https://www.sammobile.com/firmwares/");
            Button samfwButton = new Button("SamFw") { WidthRequest = 150 };
            samfwButton.Clicked += (sender, e) => OpenUrl("https://samfw.com/");
            Button frijaButton = new Button("Frija (GitHub)") { WidthRequest = 150 };
            frijaButton.Clicked += (sender, e) => OpenUrl("https://github.com/cheburator/Frija");

            firmwareBox.PackStart(samMobileButton, false, false, 0);
            firmwareBox.PackStart(samfwButton, false, false, 0);
            firmwareBox.PackStart(frijaButton, false, false, 0);

            firmwareFrame.Add(firmwareBox);
            otherBox.PackStart(firmwareFrame, false, false, 10);

            // Section 2: GitHub and Support Links
            Frame supportFrame = new Frame();
            Label supportFrameLabel = new Label
            {
                Markup = "<b>GitHub and Support</b>",
                Xalign = 0.5f // Center align the label horizontally
            };
            supportFrame.LabelWidget = supportFrameLabel;
            Box supportBox = new Box(Orientation.Vertical, 5);

            Button githubButton = new Button("GitHub Repository") { WidthRequest = 150 };
            githubButton.Clicked += (sender, e) => OpenUrl("https://github.com/daglaroglou/Aesir");
            Button issuesButton = new Button("Report an Issue") { WidthRequest = 150 };
            issuesButton.Clicked += (sender, e) => OpenUrl("https://github.com/daglaroglou/Aesir/issues");
            Button supportButton = new Button("Developer's GitHub") { WidthRequest = 150 };
            supportButton.Clicked += (sender, e) => OpenUrl("https://github.com/daglaroglou");

            supportBox.PackStart(githubButton, false, false, 0);
            supportBox.PackStart(issuesButton, false, false, 0);
            supportBox.PackStart(supportButton, false, false, 0);

            supportFrame.Add(supportBox);
            otherBox.PackStart(supportFrame, false, false, 10);

            // Section 3: Donate Options
            Frame donateFrame = new Frame();
            Label donateFrameLabel = new Label
            {
                Markup = "<b>Donate</b>",
                Xalign = 0.5f // Center align the label horizontally
            };
            donateFrame.LabelWidget = donateFrameLabel;
            Box donateBox = new Box(Orientation.Vertical, 5);

            Button paypalButton = new Button("Donate via PayPal") { WidthRequest = 150 };
            paypalButton.Clicked += (sender, e) => OpenUrl("https://paypal.me/daglaroglou");
            Button buyMeCoffeeButton = new Button("Donate via GitHub") { WidthRequest = 150 };
            buyMeCoffeeButton.Clicked += (sender, e) => OpenUrl("https://github.com/sponsors/daglaroglou");

            donateBox.PackStart(paypalButton, false, false, 0);
            donateBox.PackStart(buyMeCoffeeButton, false, false, 0);

            donateFrame.Add(donateBox);
            otherBox.PackStart(donateFrame, false, false, 10);

            return otherBox;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start("xdg-open", url);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open URL: {url}. Error: {ex}");
                Helper.DisplayErrorMessage($"Failed to open URL: {url}\n\nError: {ex.Message}");
            }
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

        // ADB-related methods
        private static async Task RefreshAdbDevices()
        {
            try
            {
                AppendAdbLog("Refreshing ADB devices...");
                string result = await Helper.ExecuteCommand("adb", "devices -l");
                AppendAdbLog("Connected ADB devices:");
                AppendAdbLog(result);
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error refreshing ADB devices: {ex.Message}");
            }
        }

        private static async Task GetDeviceInfo()
        {
            try
            {
                AppendAdbLog("Getting device information...");
                
                // Get device model
                string model = await Helper.ExecuteCommand("adb", "shell getprop ro.product.model");
                AppendAdbLog($"Model: {model}");
                
                // Get Android version
                string androidVersion = await Helper.ExecuteCommand("adb", "shell getprop ro.build.version.release");
                AppendAdbLog($"Android Version: {androidVersion}");
                
                // Get API level
                string apiLevel = await Helper.ExecuteCommand("adb", "shell getprop ro.build.version.sdk");
                AppendAdbLog($"API Level: {apiLevel}");
                
                // Get manufacturer
                string manufacturer = await Helper.ExecuteCommand("adb", "shell getprop ro.product.manufacturer");
                AppendAdbLog($"Manufacturer: {manufacturer}");
                
                // Get device codename
                string codename = await Helper.ExecuteCommand("adb", "shell getprop ro.product.name");
                AppendAdbLog($"Codename: {codename}");
                
                // Get serial number
                string serial = await Helper.ExecuteCommand("adb", "get-serialno");
                AppendAdbLog($"Serial Number: {serial}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error getting device info: {ex.Message}");
            }
        }

        private static async Task GetBatteryInfo()
        {
            try
            {
                AppendAdbLog("Getting battery information...");
                string result = await Helper.ExecuteCommand("adb", "shell dumpsys battery");
                AppendAdbLog("Battery Info:");
                AppendAdbLog(result);
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error getting battery info: {ex.Message}");
            }
        }

        private static async Task ListInstalledPackages()
        {
            try
            {
                AppendAdbLog("Listing installed packages...");
                string result = await Helper.ExecuteCommand("adb", "shell pm list packages");
                AppendAdbLog("Installed packages:");
                AppendAdbLog(result);
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error listing packages: {ex.Message}");
            }
        }

        private static async Task InstallPackage(string packagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    AppendAdbLog("Please specify a package path or APK file.");
                    return;
                }

                AppendAdbLog($"Installing package: {packagePath}");
                string result = await Helper.ExecuteCommand("adb", $"install \"{packagePath}\"");
                AppendAdbLog($"Install result: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error installing package: {ex.Message}");
            }
        }

        private static async Task UninstallPackage(string packageName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    AppendAdbLog("Please specify a package name.");
                    return;
                }

                AppendAdbLog($"Uninstalling package: {packageName}");
                string result = await Helper.ExecuteCommand("adb", $"uninstall {packageName}");
                AppendAdbLog($"Uninstall result: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error uninstalling package: {ex.Message}");
            }
        }

        private static async Task PushFile(string localPath, string remotePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localPath) || string.IsNullOrWhiteSpace(remotePath))
                {
                    AppendAdbLog("Please specify both local and remote paths.");
                    return;
                }

                AppendAdbLog($"Pushing file from {localPath} to {remotePath}");
                string result = await Helper.ExecuteCommand("adb", $"push \"{localPath}\" \"{remotePath}\"");
                AppendAdbLog($"Push result: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error pushing file: {ex.Message}");
            }
        }

        private static async Task PullFile(string remotePath, string localPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remotePath) || string.IsNullOrWhiteSpace(localPath))
                {
                    AppendAdbLog("Please specify both remote and local paths.");
                    return;
                }

                AppendAdbLog($"Pulling file from {remotePath} to {localPath}");
                string result = await Helper.ExecuteCommand("adb", $"pull \"{remotePath}\" \"{localPath}\"");
                AppendAdbLog($"Pull result: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error pulling file: {ex.Message}");
            }
        }

        private static async Task RebootDevice()
        {
            try
            {
                AppendAdbLog("Rebooting device...");
                string result = await Helper.ExecuteCommand("adb", "reboot");
                AppendAdbLog($"Reboot command sent: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error rebooting device: {ex.Message}");
            }
        }

        private static async Task RebootToBootloader()
        {
            try
            {
                AppendAdbLog("Rebooting device to bootloader...");
                string result = await Helper.ExecuteCommand("adb", "reboot bootloader");
                AppendAdbLog($"Reboot to bootloader command sent: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error rebooting to bootloader: {ex.Message}");
            }
        }

        private static async Task RebootToRecovery()
        {
            try
            {
                AppendAdbLog("Rebooting device to recovery...");
                string result = await Helper.ExecuteCommand("adb", "reboot recovery");
                AppendAdbLog($"Reboot to recovery command sent: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error rebooting to recovery: {ex.Message}");
            }
        }

        private static async Task RebootToDownload()
        {
            try
            {
                AppendAdbLog("Rebooting device to download mode...");
                string result = await Helper.ExecuteCommand("adb", "reboot download");
                AppendAdbLog($"Reboot to download mode command sent: {result}");
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error rebooting to download mode: {ex.Message}");
            }
        }

        private static async Task StartLogcat()
        {
            try
            {
                AppendAdbLog("Starting logcat (showing last 100 lines)...");
                string result = await Helper.ExecuteCommand("adb", "logcat -d -t 100");
                AppendAdbLog("Logcat output:");
                AppendAdbLog(result);
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error starting logcat: {ex.Message}");
            }
        }

        private static async Task ExecuteShellCommand(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    AppendAdbLog("Please enter a shell command.");
                    return;
                }

                AppendAdbLog($"Executing shell command: {command}");
                string result = await Helper.ExecuteCommand("adb", $"shell {command}");
                AppendAdbLog($"Shell command result:");
                AppendAdbLog(result);
            }
            catch (Exception ex)
            {
                AppendAdbLog($"Error executing shell command: {ex.Message}");
            }
        }

        private static void AppendAdbLog(string message)
        {
            Application.Invoke(delegate
            {
                if (adbLogTextView != null)
                {
                    TextBuffer buffer = adbLogTextView.Buffer;
                    TextIter endIter = buffer.EndIter;
                    string timestamp = DateTime.Now.ToString("  [HH:mm] > ");
                    buffer.Insert(ref endIter, timestamp + message + "\n");

                    // Create a tag for smaller font size
                    TextTag tag = new TextTag(null);
                    tag.SizePoints = 10;
                    buffer.TagTable.Add(tag);

                    // Apply the tag to the newly inserted text
                    buffer.ApplyTag(tag, buffer.StartIter, buffer.EndIter);

                    // Auto-scroll to the bottom
                    adbLogTextView.ScrollToIter(buffer.EndIter, 0, false, 0, 0);
                }
            });
        }

        private static void SaveAdbLogsToFile()
        {
            try
            {
                // Open a file chooser dialog to select the save location
                FileChooserDialog fileChooser = new FileChooserDialog(
                    "Save ADB Logs",
                    mainWindow,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept
                );

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    string filePath = fileChooser.Filename;
                    fileChooser.Destroy();

                    // Get the logs from the ADB TextView
                    string logs = adbLogTextView?.Buffer?.Text ?? string.Empty;

                    // Write the logs to the selected file
                    System.IO.File.WriteAllText(filePath, logs);

                    // Display a success message
                    AppendAdbLog("ADB logs saved successfully.");
                }
                else
                {
                    fileChooser.Destroy();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving ADB logs: {ex}");
                Helper.DisplayErrorMessage($"Error saving ADB logs: {ex.Message}");
            }
        }
    }
}
