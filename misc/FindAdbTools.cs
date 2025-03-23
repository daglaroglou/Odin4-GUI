using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Aesir
{
    /// <summary>
    /// Utility class to check for ADB tools
    /// </summary>
    public class FindAdbTools
    {
        private static readonly string[] AdbPaths = {
            "/bin/adb",
            "/usr/bin/adb",
            "/usr/local/bin/adb"
        };

        private static readonly string[] FastbootPaths = {
            "/bin/fastboot",
            "/usr/bin/fastboot",
            "/usr/local/bin/fastboot"
        };

        /// <summary>
        /// Checks if ADB is installed and available
        /// </summary>
        public static async Task<bool> ADBFound()
        {
            // First check for the ADB binary in common locations
            foreach (string path in AdbPaths)
            {
                if (File.Exists(path))
                    return true;
            }

            // If not found in the common locations, try to find it in PATH
            return await CommandExists("adb");
        }

        /// <summary>
        /// Checks if Fastboot is installed and available
        /// </summary>
        public static async Task<bool> FastbootFound()
        {
            // First check for the Fastboot binary in common locations
            foreach (string path in FastbootPaths)
            {
                if (File.Exists(path))
                    return true;
            }

            // If not found in the common locations, try to find it in PATH
            return await CommandExists("fastboot");
        }

        /// <summary>
        /// Checks if a command exists in the system PATH
        /// </summary>
        private static async Task<bool> CommandExists(string command)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = "which";
                process.StartInfo.Arguments = command;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}