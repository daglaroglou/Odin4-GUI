using System;
using System.Diagnostics;

namespace Odin4GUI
{
    public class GetAdbTools
    {
        /// <summary>
        /// Detects the Linux distribution and installs ADB tools accordingly
        /// </summary>
        public static async Task<bool> DownloadADB()
        {
            try
            {
                // Detect package manager
                if (await CommandExists("apt"))
                {
                    // Debian/Ubuntu
                    return await InstallWithApt();
                }
                else if (await CommandExists("dnf"))
                {
                    // Fedora/RHEL
                    return await InstallWithDnf();
                }
                else if (await CommandExists("pacman"))
                {
                    // Arch Linux
                    return await InstallWithPacman();
                }
                else if (await CommandExists("zypper"))
                {
                    // OpenSUSE
                    return await InstallWithZypper();
                }
                else
                {
                    throw new NotSupportedException("Unsupported Linux distribution. Please install ADB tools manually.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error installing ADB tools: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs ADB tools using apt (Debian/Ubuntu)
        /// </summary>
        private static async Task<bool> InstallWithApt()
        {
            // First update package lists
            await ExecuteCommand("sudo", "apt update");

            // Then install ADB tools
            int exitCode = await ExecuteCommandWithExitCode("sudo", "apt install -y adb fastboot");
            return exitCode == 0;
        }

        /// <summary>
        /// Installs ADB tools using dnf (Fedora/RHEL)
        /// </summary>
        private static async Task<bool> InstallWithDnf()
        {
            int exitCode = await ExecuteCommandWithExitCode("sudo", "dnf install -y android-tools");
            return exitCode == 0;
        }

        /// <summary>
        /// Installs ADB tools using pacman (Arch Linux)
        /// </summary>
        private static async Task<bool> InstallWithPacman()
        {
            int exitCode = await ExecuteCommandWithExitCode("sudo", "pacman -S --noconfirm android-tools");
            return exitCode == 0;
        }

        /// <summary>
        /// Installs ADB tools using zypper (OpenSUSE)
        /// </summary>
        private static async Task<bool> InstallWithZypper()
        {
            int exitCode = await ExecuteCommandWithExitCode("sudo", "zypper install -y android-tools");
            return exitCode == 0;
        }

        /// <summary>
        /// Checks if a command exists in the system
        /// </summary>
        private static async Task<bool> CommandExists(string command)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo.FileName = "which";
                process.StartInfo.Arguments = command;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
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

        /// <summary>
        /// Executes a command and returns the output
        /// </summary>
        private static async Task<string> ExecuteCommand(string command, string arguments)
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

            if (process.ExitCode != 0)
                Console.Error.WriteLine($"Command execution failed: {error}");

            return output.Trim();
        }

        /// <summary>
        /// Executes a command and returns the exit code
        /// </summary>
        private static async Task<int> ExecuteCommandWithExitCode(string command, string arguments)
        {
            using Process process = new Process();
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    }
}