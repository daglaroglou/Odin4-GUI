using System.Diagnostics;

namespace Odin4GUI
{
    /// <summary>
    /// Utility class to check for Odin
    /// </summary>
    public class FindOdin
    {
        private static readonly string[] OdinPaths = {
            "/bin/odin4",
            "/usr/bin/odin4",
            "/usr/local/bin/odin4"
        };

        /// <summary>
        /// Checks if Odin4 is installed and available
        /// </summary>
        public static async Task<bool> OdinFound()
        {
            // First check for the Odin binary in common locations
            foreach (string path in OdinPaths)
            {
                if (File.Exists(path))
                    return true;
            }

            // If not found in the common locations, try to find it in PATH
            return await CommandExists("odin4");
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