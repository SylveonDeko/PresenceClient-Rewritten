using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PresenceClient.ViewModels;
using PresenceClient.Platform;

namespace PresenceClient.Views;
public partial class MainWindow : Window
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        if (PlatformHelper.IsMacOS)
        {
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.OSXThickTitleBar;
        }

        viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        viewModel.ShowMainWindowRequested += (sender, args) => ShowMainWindow();

        SetupPlatformSpecifics();
    }

    private void SetupPlatformSpecifics()
    {
        if (PlatformHelper.IsMacOS)
        {
            var mainMenu = new NativeMenu();
            var appMenu = new NativeMenu();
            var appMenuItem = new NativeMenuItem("PresenceClient");
            appMenuItem.Menu = appMenu;

            appMenu.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("Quit PresenceClient");
            quitItem.Click += (sender, e) => viewModel?.ExitApplication();
            appMenu.Add(quitItem);

            mainMenu.Add(appMenuItem);
            NativeMenu.SetMenu(this, mainMenu);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (viewModel is { MinimizeToTray: true } && PlatformHelper.CanUseTrayIcon())
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            base.OnClosing(e);
            viewModel?.ExitApplication(); // This will properly clean up and exit
        }
    }

    public void ShowMainWindow()
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.WindowState = WindowState.Normal;
        }

        this.Show();
        this.Activate();

        if (!PlatformHelper.IsMacOS)  // macOS handles window focusing differently
        {
            this.Topmost = true;
            this.Topmost = false;
        }

        this.Focus();
    }
}