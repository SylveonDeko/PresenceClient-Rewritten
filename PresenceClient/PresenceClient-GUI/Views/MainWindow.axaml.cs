using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PresenceClient.ViewModels;

namespace PresenceClient.Views;

public partial class MainWindow : Window, IDisposable
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        viewModel.ShowMainWindowRequested += (sender, args) => ShowMainWindow();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (viewModel != null && viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            base.OnClosing(e);
            Dispose();
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
        this.Topmost = true;
        this.Topmost = false;
        this.Focus();
    }

    public void Dispose()
    {
        if (viewModel != null)
        {
            viewModel.Dispose();
            viewModel = null;
        }
    }
}