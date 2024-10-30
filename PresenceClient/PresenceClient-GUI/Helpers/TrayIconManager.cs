using Avalonia.Controls;
using Avalonia.Platform;
using System;
using Avalonia.Threading;
using PresenceClient.ViewModels;

namespace PresenceClient.Helpers
{
    public class TrayIconManager : IDisposable
    {
        private readonly MainWindowViewModel viewModel;
        private TrayIcon? trayIcon;
        private NativeMenuItem? connectMenuItem;
        private bool disposed;
        private bool isEnabled;

        public TrayIconManager(MainWindowViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public void EnableTrayIcon(bool enable)
        {
            if (disposed || enable == isEnabled) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (enable)
                {
                    InitializeTrayIcon();
                }
                else
                {
                    DisposeTrayIcon();
                }
                isEnabled = enable;
            });
        }

        private void InitializeTrayIcon()
        {
            if (trayIcon != null || disposed) return;

            trayIcon = new TrayIcon();
            var menu = new NativeMenu();

            var showItem = new NativeMenuItem("Show");
            showItem.Click += ShowItem_Click;
            menu.Add(showItem);

            connectMenuItem = new NativeMenuItem("Connect");
            connectMenuItem.Click += ConnectMenuItem_Click;
            menu.Add(connectMenuItem);

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += ExitItem_Click;
            menu.Add(exitItem);

            trayIcon.Menu = menu;
            trayIcon.Clicked += TrayIcon_Clicked;

            UpdateIcon(false);
            trayIcon.IsVisible = true;
        }

        private void ShowItem_Click(object? sender, EventArgs e)
        {
            viewModel.ShowMainWindow();
        }

        private void ConnectMenuItem_Click(object? sender, EventArgs e)
        {
            viewModel.ToggleConnection();
        }

        private void ExitItem_Click(object? sender, EventArgs e)
        {
            viewModel.ExitApplication();
        }

        private void TrayIcon_Clicked(object? sender, EventArgs e)
        {
            viewModel.ShowMainWindow();
        }

        public void UpdateIcon(bool isConnected)
        {
            if (disposed) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (trayIcon == null || connectMenuItem == null) return;

                var iconName = isConnected ? "Connected.ico" : "Disconnected.ico";
                using var assets = AssetLoader.Open(new Uri($"avares://PresenceClient-GUI/Assets/{iconName}"));

                trayIcon.Icon = new WindowIcon(assets);
                trayIcon.ToolTipText = $"PresenceClient ({(isConnected ? "Connected" : "Disconnected")})";
                connectMenuItem.Header = isConnected ? "Disconnect" : "Connect";
            });
        }

        private void DisposeTrayIcon()
        {
            if (trayIcon == null) return;

            trayIcon.IsVisible = false;
            trayIcon.Clicked -= TrayIcon_Clicked;

            if (trayIcon.Menu != null)
            {
                foreach (var item in trayIcon.Menu.Items)
                {
                    if (item is not NativeMenuItem menuItem) continue;
                    menuItem.Click -= ShowItem_Click;
                    menuItem.Click -= ConnectMenuItem_Click;
                    menuItem.Click -= ExitItem_Click;
                }
            }

            trayIcon.Dispose();
            trayIcon = null;
            connectMenuItem = null;
        }

        public void Dispose()
        {
            if (disposed) return;

            Dispatcher.UIThread.Post(DisposeTrayIcon);
            disposed = true;
        }
    }
}