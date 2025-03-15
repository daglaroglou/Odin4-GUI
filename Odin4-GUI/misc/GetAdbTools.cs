using System.Diagnostics;

namespace GetAdbTools
{
    public class GetAdbTools
    {
        public static async Task Download()
        {
            if (await CommandExists("apt"))
            {
                await InstallPackages("sudo apt install -y adb fastboot");
            }
            else if (await CommandExists("pacman"))
            {
                await InstallPackages("sudo pacman -S --noconfirm android-tools");
            }
            else
            {
                throw new NotSupportedException("Unsupported Linux distribution");
            }
        }

        private static async Task<bool> CommandExists(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "sh";
                process.StartInfo.Arguments = $"-c \"command -v {command}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                string result = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return !string.IsNullOrEmpty(result);
            }
        }

        private static async Task InstallPackages(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "sh";
                process.StartInfo.Arguments = $"-c \"{command}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Error installing packages: {error}");
                }
            }
        }
    }
}