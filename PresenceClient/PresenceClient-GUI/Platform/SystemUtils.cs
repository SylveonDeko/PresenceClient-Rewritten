using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PresenceClient.Platform
{
    public static class SystemUtils
    {
        public static async Task SetAutoStartAsync(bool enable)
        {
            try
            {
                if (PlatformHelper.IsWindows)
                {
                    await SetWindowsAutoStartAsync(enable);
                }
                else if (PlatformHelper.IsMacOS)
                {
                    await SetMacOSAutoStartAsync(enable);
                }
                else if (PlatformHelper.IsLinux)
                {
                    await SetLinuxAutoStartAsync(enable);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting auto start: {ex.Message}");
            }
        }

        private static async Task SetWindowsAutoStartAsync(bool enable)
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = Path.Combine(startupFolder, "PresenceClient.lnk");
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                         Path.Combine(AppContext.BaseDirectory, "PresenceClient.exe");

            if (enable)
            {
                var shellLink = (IShellLink)new ShellLink();
                shellLink.SetPath(exePath);
                shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath));

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, false);

                Marshal.ReleaseComObject(shellLink);
                Marshal.ReleaseComObject(persistFile);
            }
            else
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010B-0000-0000-C000-000000000046")]
        internal interface IPersistFile
        {
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile);
            void IsDirty();
            void Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        }

        private static async Task SetMacOSAutoStartAsync(bool enable)
        {
            var plistPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/LaunchAgents/com.presenceclient.app.plist");

            if (enable)
            {
                var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.presenceclient.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{Process.GetCurrentProcess().MainModule?.FileName}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";
                await File.WriteAllTextAsync(plistPath, plistContent);
                await RunCommandAsync("launchctl", $"load {plistPath}");
            }
            else
            {
                if (File.Exists(plistPath))
                {
                    await RunCommandAsync("launchctl", $"unload {plistPath}");
                    File.Delete(plistPath);
                }
            }
        }

        private static async Task SetLinuxAutoStartAsync(bool enable)
        {
            var autostartDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart");
            var desktopFile = Path.Combine(autostartDir, "presenceclient.desktop");

            if (enable)
            {
                Directory.CreateDirectory(autostartDir);
                var desktopEntry = $@"[Desktop Entry]
Type=Application
Name=PresenceClient
Exec={Process.GetCurrentProcess().MainModule?.FileName}
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true";
                await File.WriteAllTextAsync(desktopFile, desktopEntry);
            }
            else
            {
                if (File.Exists(desktopFile))
                {
                    File.Delete(desktopFile);
                }
            }
        }

        private static async Task<string> RunCommandAsync(string command, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
    }
}