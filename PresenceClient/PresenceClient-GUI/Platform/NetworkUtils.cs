using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PresenceClient.Platform
{
    public static class NetworkUtils
    {
        public static string GetMacByIp(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return string.Empty;

            try
            {
                if (PlatformHelper.IsWindows)
                    return GetWindowsMacByIp(ip);
                else
                    return GetUnixMacByIp(ip);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting MAC address: {ex.Message}");
                return string.Empty;
            }
        }

        public static string GetIpByMac(string mac)
        {
            if (string.IsNullOrEmpty(mac))
                return string.Empty;

            try
            {
                if (PlatformHelper.IsWindows)
                    return GetWindowsIpByMac(mac);
                else
                    return GetUnixIpByMac(mac);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting IP address: {ex.Message}");
                return string.Empty;
            }
        }

        private static string GetWindowsMacByIp(string ip)
        {
            var output = RunCommand("arp", $"-a {ip}");
            var match = Regex.Match(output, @"(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}");
            return match.Success ? match.Value.Replace("-", ":").ToLower() : string.Empty;
        }

        private static string GetWindowsIpByMac(string mac)
        {
            var output = RunCommand("arp", "-a");
            mac = mac.Replace(":", "-").ToUpper();
            var match = Regex.Match(output, $@"([0-9.]{{7,15}})\s+{mac}");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string GetUnixMacByIp(string ip)
        {
            string command, arguments;
            if (PlatformHelper.IsMacOS)
            {
                command = "arp";
                arguments = $"-n {ip}";
            }
            else // Linux
            {
                command = "ip";
                arguments = $"neigh show {ip}";
            }

            var output = RunCommand(command, arguments);
            var match = Regex.Match(output, @"(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}");
            return match.Success ? match.Value.ToLower() : string.Empty;
        }

        private static string GetUnixIpByMac(string mac)
        {
            string command, arguments;
            if (PlatformHelper.IsMacOS)
            {
                command = "arp";
                arguments = "-an";
            }
            else // Linux
            {
                command = "ip";
                arguments = "neigh show";
            }

            var output = RunCommand(command, arguments);
            mac = mac.ToLower();
            var match = Regex.Match(output, $@"([0-9.]{{7,15}}).*{mac}");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string RunCommand(string command, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running command {command}: {ex.Message}");
                return string.Empty;
            }
        }

        public static bool IsValidMacAddress(string mac)
        {
            return !string.IsNullOrEmpty(mac) &&
                   Regex.IsMatch(mac, "^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$");
        }

        public static bool IsValidIpAddress(string ip)
        {
            return !string.IsNullOrEmpty(ip) &&
                   Regex.IsMatch(ip, @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        }
    }
}