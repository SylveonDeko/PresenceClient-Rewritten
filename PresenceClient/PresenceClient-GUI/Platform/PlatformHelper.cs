using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PresenceClient.Platform;

public static class PlatformHelper
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static string GetConfigDirectory()
    {
        if (IsWindows)
            return AppContext.BaseDirectory;
        else if (IsMacOS)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Application Support/PresenceClient");
        else // Linux
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/presenceclient");
    }

    public static string GetConfigPath()
    {
        EnsureConfigDirectory();
        return Path.Combine(GetConfigDirectory(), "Config.json");
    }

    public static string GetResourcePath(string filename)
    {
        if (IsWindows)
            return Path.Combine(AppContext.BaseDirectory, "Assets", filename);
        else if (IsMacOS)
            return Path.Combine(AppContext.BaseDirectory, "Contents", "Resources", filename);
        else // Linux
            return Path.Combine("/usr/share/presenceclient", filename);
    }

    public static void EnsureConfigDirectory()
    {
        var configDir = GetConfigDirectory();
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    public static bool CanUseTrayIcon()
    {
        if (IsWindows)
            return true;
        if (IsMacOS)
            return true;
        if (IsLinux)
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));
        return false;
    }
}