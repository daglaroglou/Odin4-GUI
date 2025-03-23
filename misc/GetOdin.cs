using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq;

namespace Aesir
{
    public class GetOdin
    {
        private const string GithubApiUrl = "https://api.github.com/repos/Adrilaw/OdinV4/releases/latest";
        private const string TempZipPath = "/tmp/odin.zip";
        private const string ExtractPath = "/tmp/odin";
        private const string BinPath = "/bin/odin4";

        /// <summary>
        /// Downloads the latest version of Odin from GitHub and installs it
        /// </summary>
        public static async Task<bool> Download()
        {
            try
            {
                Console.WriteLine("Starting Odin download process...");

                // Create the temporary directory if it doesn't exist
                if (Path.GetDirectoryName(TempZipPath) is string tempDir && !string.IsNullOrEmpty(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // Get download URL from GitHub
                string downloadUrl = await GetLatestReleaseUrl();
                Console.WriteLine($"Found download URL: {downloadUrl}");

                // Download the file
                await DownloadFile(downloadUrl, TempZipPath);
                Console.WriteLine($"Downloaded file to {TempZipPath}");

                // Extract and install
                await ExtractAndInstall();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error downloading Odin: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the download URL for the latest release from GitHub
        /// </summary>
        private static async Task<string> GetLatestReleaseUrl()
        {
            using HttpClient client = new HttpClient();
            // Set a user agent to avoid GitHub API rate limiting
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Odin4GUI/1.0");

            string response = await client.GetStringAsync(GithubApiUrl);
            var json = JObject.Parse(response);

            // Get the assets array
            var assets = json["assets"] as JArray;
            if (assets == null || assets.Count == 0)
                throw new Exception("No assets found in the release");

            // Get the first asset's download URL
            string? downloadUrl = assets[0]["browser_download_url"]?.ToString();
            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception("No download URL found for the asset");

            return downloadUrl;
        }

        /// <summary>
        /// Downloads a file from the specified URL
        /// </summary>
        private static async Task DownloadFile(string url, string destinationPath)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Odin4GUI/1.0");

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }

        /// <summary>
        /// Extracts the downloaded archive and installs Odin
        /// </summary>
        private static async Task ExtractAndInstall()
        {
            // Clean existing extract directory if it exists
            if (Directory.Exists(ExtractPath))
            {
                Console.WriteLine($"Deleting existing extract directory: {ExtractPath}");
                Directory.Delete(ExtractPath, true);
            }

            Directory.CreateDirectory(ExtractPath);
            Console.WriteLine($"Created extract directory: {ExtractPath}");

            try
            {
                // Extract the zip file
                Console.WriteLine($"Extracting: {TempZipPath} to {ExtractPath}");
                ZipFile.ExtractToDirectory(TempZipPath, ExtractPath);

                // List all files in the extract directory for debugging
                Console.WriteLine("Extracted files:");
                ListAllFiles(ExtractPath);

                // Use a more robust approach to find the Odin executable
                string? odinExecutablePath = FindOdinExecutable(ExtractPath);

                if (string.IsNullOrEmpty(odinExecutablePath))
                    throw new FileNotFoundException("Odin executable not found in the extracted files");

                Console.WriteLine($"Found Odin executable at: {odinExecutablePath}");

                // Make the executable executable
                await ExecuteCommand("chmod", $"+x \"{odinExecutablePath}\"");
                Console.WriteLine("Set executable permissions");

                // Create directory if it doesn't exist
                string? binDirectory = Path.GetDirectoryName(BinPath);
                if (!string.IsNullOrEmpty(binDirectory) && !Directory.Exists(binDirectory))
                {
                    Console.WriteLine($"Creating directory: {binDirectory}");
                    Directory.CreateDirectory(binDirectory);
                }

                // Move the executable to the bin directory
                if (File.Exists(BinPath))
                {
                    Console.WriteLine($"Deleting existing file: {BinPath}");
                    File.Delete(BinPath);
                }

                Console.WriteLine($"Moving file from {odinExecutablePath} to {BinPath}");
                File.Copy(odinExecutablePath, BinPath);

                // Clean up
                Console.WriteLine("Cleaning up temporary files");
                if (File.Exists(TempZipPath))
                    File.Delete(TempZipPath);

                if (Directory.Exists(ExtractPath))
                    Directory.Delete(ExtractPath, true);

                Console.WriteLine("Odin downloaded and installed successfully to /bin/odin4");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during extraction and installation: {ex.Message}");
                throw new Exception($"Failed to extract and install Odin: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lists all files in a directory and its subdirectories recursively
        /// </summary>
        private static void ListAllFiles(string path)
        {
            try
            {
                // List all files in the directory
                foreach (string file in Directory.GetFiles(path))
                {
                    Console.WriteLine($"  File: {file}");
                }

                // Recursively list files in subdirectories
                foreach (string directory in Directory.GetDirectories(path))
                {
                    Console.WriteLine($"  Directory: {directory}");
                    ListAllFiles(directory);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error listing files: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the Odin executable in the extracted files
        /// </summary>
        private static string? FindOdinExecutable(string rootPath)
        {
            try
            {
                // Try to find a file named exactly "odin" or "odin4"
                string[] exactMatches = Directory.GetFiles(rootPath, "odin", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(rootPath, "odin4", SearchOption.AllDirectories))
                    .ToArray();

                if (exactMatches.Length > 0)
                    return exactMatches[0];

                // If no exact matches, look for files containing "odin" in the name
                string[] possibleMatches = Directory.GetFiles(rootPath, "*odin*", SearchOption.AllDirectories);

                if (possibleMatches.Length == 0)
                    return null;

                // Check if any of these files are ELF executables
                foreach (string file in possibleMatches)
                {
                    if (IsElfExecutable(file))
                        return file;
                }

                // If no ELF executable found, return the first match as a fallback
                return possibleMatches[0];
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error finding Odin executable: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a file is an ELF executable
        /// </summary>
        private static bool IsElfExecutable(string filePath)
        {
            try
            {
                using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[4];
                int bytesRead = fs.Read(buffer, 0, 4);

                // Check for ELF magic number (0x7F 'E' 'L' 'F')
                return bytesRead == 4 && buffer[0] == 0x7F && buffer[1] == 'E' && buffer[2] == 'L' && buffer[3] == 'F';
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executes a shell command
        /// </summary>
        private static async Task<string> ExecuteCommand(string command, string arguments)
        {
            using var process = new Process();
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

            if (process.ExitCode != 0)
                throw new Exception($"Command execution failed: {error}");

            return output.Trim();
        }

        /// <summary>
        /// Alternative manual download approach if GitHub API fails
        /// </summary>
        public static async Task<bool> DownloadDirect()
        {
            try
            {
                // Direct download URL as fallback
                const string directUrl = "https://github.com/Adrilaw/OdinV4/releases/latest/download/odin4-linux.zip";

                Console.WriteLine($"Attempting direct download from: {directUrl}");
                await DownloadFile(directUrl, TempZipPath);
                await ExtractAndInstall();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Direct download failed: {ex.Message}");
                return false;
            }
        }
    }
}