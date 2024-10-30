using System;
using Avalonia;
using Avalonia.ReactiveUI;
using PresenceClient.Platform;

namespace PresenceClient
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application crashed: {ex}");
                throw;
            }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .UseReactiveUI();

            if (PlatformHelper.IsLinux)
            {
                builder.With(new X11PlatformOptions
                {
                    EnableMultiTouch = true,
                    UseDBusMenu = true,
                    UseDBusFilePicker = true,
                    RenderingMode =
                    [
                        X11RenderingMode.Glx,
                        X11RenderingMode.Egl,
                        X11RenderingMode.Software
                    ]
                });
            }
            else if (PlatformHelper.IsWindows)
            {
                builder.With(new Win32PlatformOptions
                {
                    RenderingMode =
                    [
                        Win32RenderingMode.AngleEgl,
                        Win32RenderingMode.Wgl,
                        Win32RenderingMode.Software
                    ],
                    CompositionMode =
                    [
                        Win32CompositionMode.WinUIComposition,
                        Win32CompositionMode.DirectComposition,
                        Win32CompositionMode.RedirectionSurface
                    ],
                    DpiAwareness = Win32DpiAwareness.PerMonitorDpiAware
                });
            }
            else if (PlatformHelper.IsMacOS)
            {
                builder.With(new MacOSPlatformOptions
                {
                    ShowInDock = true,
                    DisableDefaultApplicationMenuItems = false,
                    DisableNativeMenus = false,
                    DisableSetProcessName = false,
                });
            }

#if DEBUG
            builder.LogToTrace();
#endif

            return builder;
        }
    }
}