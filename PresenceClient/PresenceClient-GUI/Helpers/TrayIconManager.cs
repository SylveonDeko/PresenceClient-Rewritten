using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PresenceClient.ViewModels;

namespace PresenceClient.Helpers;

public class TrayIconManager : IDisposable
{
    private TrayIcon? trayIcon;
    private readonly MainWindowViewModel _viewModel;
    private NativeMenuItem connectMenuItem;

    public TrayIconManager(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        Dispatcher.UIThread.Post(() =>
        {
            trayIcon = new TrayIcon();

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem("Show");
            showItem.Click += (sender, e) => _viewModel.ShowMainWindow();
            menu.Add(showItem);

            connectMenuItem = new NativeMenuItem("Connect");
            connectMenuItem.Click += (sender, e) => _viewModel.ToggleConnection();
            menu.Add(connectMenuItem);

            var exitItem = new NativeMenuItem("Exit");
            exitItem.Click += (sender, e) => _viewModel.ExitApplication();
            menu.Add(exitItem);

            trayIcon.Menu = menu;
            trayIcon.Clicked += TrayIcon_Clicked;

            UpdateIcon(false); // Start with disconnected icon
            trayIcon.IsVisible = true;
        });
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        _viewModel.ShowMainWindow();
    }

    public void UpdateIcon(bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (trayIcon == null) return;
            var iconName = isConnected ? "Connected.ico" : "Disconnected.ico";
            var assets = AssetLoader.Open(new Uri($"avares://PresenceClient-GUI/Assets/{iconName}"));

            trayIcon.Icon = new WindowIcon(assets);
            trayIcon.ToolTipText = $"PresenceClient ({(isConnected ? "Connected" : "Disconnected")})";

            connectMenuItem.Header = isConnected ? "Disconnect" : "Connect";
        });
    }

    public void Dispose()
    {
        Dispatcher.UIThread.Post(() =>
        {
            trayIcon?.Dispose();
        });
    }
}