using Gtk;
using Odin4GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Odin4_GUI.misc
{
    public static class Helper
    {
        // Device monitoring
        private static System.Timers.Timer deviceCheckTimer;
        private static bool isDeviceConnected = false;
        private static string connectedDeviceId = string.Empty;

        // Thread synchronization
        private static readonly object logLock = new object();
        public static SynchronizationContext UiSynchronizationContext { get; set; }
        public static ManualResetEvent UiInitializedEvent { get; } = new ManualResetEvent(false);
        public static CancellationTokenSource CancellationTokenSource { get; set; }

        // UI reference for logging
        private static TextView logTextView;

        public static void SetLogTextView(TextView textView)
        {
            logTextView = textView;
        }

        public static void AppendLog(string message)
        {
            // Use the UI synchronization context to update the UI
            Application.Invoke(delegate
            {
                lock (logLock)
                {
                    if (logTextView != null)
                    {
                        TextBuffer buffer = logTextView.Buffer;
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
                        logTextView.ScrollToIter(buffer.EndIter, 0, false, 0, 0);
                    }
                }
            });
        }

        public static async Task RunBackgroundTasks()
        {
            try
            {
                // Wait a bit more to ensure the UI is fully initialized
                await Task.Delay(1000);

                if (!IsRunningAsRoot())
                {
                    ShowSudoRequiredMessage();
                    return;
                }

                // Check for ADB tools
                if (!await FindAdbTools.ADBFound() || !await FindAdbTools.FastbootFound())
                {
                    AppendLog("ADB tools not found. Attempting to install...");
                    await GetAdbTools.DownloadADB();

                    if (!await FindAdbTools.ADBFound() || !await FindAdbTools.FastbootFound())
                    {
                        AppendLog("Failed to install ADB tools. Please install manually.");
                    }
                    else
                    {
                        try
                        {
                            string adbVersion = await ExecuteCommand("sh", "-c \"adb version | sed -n '2p' | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                            AppendLog($"ADB tools installed successfully. (v{adbVersion})");
                        }
                        catch (Exception)
                        {
                            AppendLog("ADB tools installed successfully, but couldn't determine version.");
                        }
                    }
                }
                else
                {
                    try
                    {
                        string adbVersion = await ExecuteCommand("sh", "-c \"adb version | sed -n '2p' | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                        AppendLog($"ADB tools found. (v{adbVersion})");
                    }
                    catch (Exception)
                    {
                        AppendLog("ADB tools found, but couldn't determine version.");
                    }
                }

                // Check for Odin
                if (!await FindOdin.OdinFound())
                {
                    AppendLog("Odin4 not found. Attempting to download...");

                    // Try the standard download first
                    bool downloadSuccess = await GetOdin.Download();

                    // If that fails, try the direct download
                    if (!downloadSuccess)
                    {
                        AppendLog("Standard download failed. Trying direct download...");
                        downloadSuccess = await GetOdin.DownloadDirect();
                    }

                    if (!await FindOdin.OdinFound())
                    {
                        AppendLog("Failed to install Odin4. Please install manually.");
                    }
                    else
                    {
                        try
                        {
                            string odinVersion = await ExecuteCommand("sh", "-c \"sudo odin4 -v | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                            AppendLog($"Odin4 installed successfully. (v{odinVersion})");
                        }
                        catch (Exception)
                        {
                            AppendLog("Odin4 installed successfully, but couldn't determine version.");
                        }
                    }
                }
                else
                {
                    try
                    {
                        string odinVersion = await ExecuteCommand("sh", "-c \"sudo odin4 -v | grep -oP '\\d+\\.\\d+\\.\\d+'\"");
                        AppendLog($"Odin4 found (v{odinVersion})");
                    }
                    catch (Exception)
                    {
                        AppendLog("Odin4 found, but couldn't determine version.");
                    }
                }

                // Initialize and start device monitoring
                deviceCheckTimer = new System.Timers.Timer(1000);
                deviceCheckTimer.Elapsed += CheckDeviceConnection;
                deviceCheckTimer.Start();
            }
            catch (Exception ex)
            {
                AppendLog($"Error in background tasks: {ex.Message}");
            }
        }

        private static void CheckDeviceConnection(object sender, ElapsedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    string output = await ExecuteCommand("sudo", "odin4 -l");
                    Match match = Regex.Match(output, @"\d{3}$");

                    if (match.Success)
                    {
                        string id = match.Value;
                        if (!isDeviceConnected)
                        {
                            isDeviceConnected = true;
                            connectedDeviceId = id;
                            AppendLog($"<ID:{id}> Connected.");
                        }
                    }
                    else
                    {
                        if (isDeviceConnected)
                        {
                            isDeviceConnected = false;
                            AppendLog($"<ID:{connectedDeviceId}> Disconnected.");
                            connectedDeviceId = string.Empty;
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently ignore errors during device check
                    // This prevents UI crashes if odin4 is not installed
                }
            });
        }

        public static bool IsRunningAsRoot()
        {
            return Environment.UserName == "root";
        }

        public static void DisplayErrorMessage(string message)
        {
            try
            {
                // Try to display a GUI message dialog if possible
                Application.Invoke(delegate
                {
                    try
                    {
                        MessageDialog md = new MessageDialog(
                            null,
                            DialogFlags.Modal,
                            MessageType.Error,
                            ButtonsType.Ok,
                            message);
                        md.Run();
                        md.Destroy();
                    }
                    catch
                    {
                        // If we can't display a GUI dialog, fall back to console
                        Console.Error.WriteLine(message);
                    }
                });
            }
            catch
            {
                // Last resort: write to console if Application.Invoke fails
                Console.Error.WriteLine(message);
            }
        }

        public static void ShowSudoRequiredMessage()
        {
            Application.Invoke(delegate
            {
                MessageDialog md = new MessageDialog(
                    null,
                    DialogFlags.Modal,
                    MessageType.Warning,
                    ButtonsType.Ok,
                    "This program requires sudo permissions to run. Please restart it with 'sudo'.");
                md.Run();
                md.Destroy();
                Application.Quit();
            });
        }

        public static async Task<string> ExecuteCommand(string command, string arguments)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    return $"{output}\nErrors: {error}".Trim();
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                AppendLog($"Command execution error: {ex.Message}");
                throw;
            }
        }

        public static async Task StartOdinProcess(Grid grid, string[] options)
        {
            try
            {
                var selectedFiles = new List<string>();

                // Loop through the grid to get selected files and options
                foreach (Widget widget in grid.Children)
                {
                    if (widget is Entry entry)
                    {
                        string filePath = entry.Text;
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            int row = (int)grid.ChildGetProperty(entry, "top-attach");
                            Widget checkButtonWidget = grid.GetChildAt(0, row);

                            if (checkButtonWidget is CheckButton checkButton && checkButton.Active)
                            {
                                string option = options[row];
                                string flag = GetFlagForOption(option);

                                if (!string.IsNullOrEmpty(flag))
                                {
                                    selectedFiles.Add($"{flag} \"{filePath}\"");
                                }
                            }
                        }
                    }
                }

                if (selectedFiles.Count == 0)
                {
                    AppendLog("No files selected.");
                    return;
                }

                // Construct the Odin command
                string arguments = string.Join(" ", selectedFiles);

                // Run the Odin command
                string result = await ExecuteCommand("sudo", $"odin4 {arguments}");
                AppendLog(result);
            }
            catch (Exception ex)
            {
                AppendLog($"Error executing Odin: {ex.Message}");
            }
        }

        private static string GetFlagForOption(string option)
        {
            return option switch
            {
                "BL" => "-b",
                "AP" => "-a",
                "CP" => "-c",
                "CSC" => "-s",
                "USERDATA" => "-u",
                _ => string.Empty
            };
        }

        public static void ResetForm(Grid grid)
        {
            try
            {
                foreach (Widget widget in grid.Children)
                {
                    if (widget is Entry entry)
                    {
                        entry.Text = string.Empty;
                    }
                    else if (widget is CheckButton checkButton)
                    {
                        checkButton.Active = false;
                    }
                }
                AppendLog("Form reset.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error resetting form: {ex.Message}");
            }
        }

        public static void BrowseForFile(Entry entry, Window parentWindow)
        {
            try
            {
                using FileChooserDialog fileChooser = new FileChooserDialog(
                    "Choose file",
                    parentWindow,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

                fileChooser.SetCurrentFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    entry.Text = fileChooser.Filename;
                    AppendLog($"Selected file: {fileChooser.Filename}");
                }

                fileChooser.Destroy();
            }
            catch (Exception ex)
            {
                AppendLog($"Error browsing for file: {ex.Message}");
            }
        }

        public static void CleanupResources()
        {
            deviceCheckTimer?.Stop();
            deviceCheckTimer?.Dispose();
            CancellationTokenSource?.Cancel();
        }
    }
}