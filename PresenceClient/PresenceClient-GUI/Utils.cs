using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PresenceClient_GUI;

public static partial class Utils
{
    public static string GetMacByIp(string ip)
    {
        var macIpPairs = GetAllMacAddressesAndIpPairs();

        return macIpPairs.FirstOrDefault(x => x.IpAddress == ip).MacAddress ?? "";
    }

    public static string GetIpByMac(string mac)
    {
        mac = mac.ToLower();
        var macIpPairs = GetAllMacAddressesAndIpPairs();

        return macIpPairs.FirstOrDefault(x => x.MacAddress == mac).IpAddress ?? "";
    }

    private static List<MacIpPair> GetAllMacAddressesAndIpPairs()
    {
        var mip = new List<MacIpPair>();
        string cmdOutput;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cmdOutput = RunCommand("arp", "-a");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            cmdOutput = RunCommand("arp", "-n");
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS");
        }

        mip.AddRange(from Match m in CmdRegex().Matches(cmdOutput)
                     select new MacIpPair
                     {
                         MacAddress = m.Groups["mac"].Value,
                         IpAddress = m.Groups["ip"].Value
                     });

        return mip;
    }

    private static string RunCommand(string command, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        return process.StandardOutput.ReadToEnd();
    }

    public struct MacIpPair
    {
        public string MacAddress;
        public string IpAddress;
    }

    [GeneratedRegex(@"(?<ip>([0-9]{1,3}\.?){4})\s+(?<mac>([a-f0-9]{2}:?){6})", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CmdRegex();
}
