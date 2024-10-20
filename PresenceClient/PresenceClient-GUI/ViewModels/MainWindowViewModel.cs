using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using DiscordRPC;
using Newtonsoft.Json;
using PresenceClient_GUI;
using PresenceClient.Helpers;
using PresenceClient.Views;
using PresenceCommon.Types;
using ReactiveUI;

namespace PresenceClient.ViewModels;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private static TrayIconManager? trayIconManager;
    private bool autoConvertIpToMac;
    private string bigImageKey = "";
    private string bigImageText = "";

    private CancellationTokenSource? cancellationTokenSource;
    private Socket? client;
    private string clientId = "";
    private bool displayHomeMenu = true;
    private bool hasSeenMacPrompt;
    private string ipAddress = "";
    private bool isConnected;
    private string lastProgramName = string.Empty;
    private bool manualUpdate;
    private bool minimizeToTray;
    private IPAddress? resolvedIpAddress;
    private DiscordRpcClient? rpc;
    private bool showTimeLapsed = true;
    private string smallImageKey = "";
    private string stateText = "";
    private string status = "";
    private Timestamps? time;

    public MainWindowViewModel()
    {
        LoadConfig();
        var canConnect = this.WhenAnyValue(x => x.IsConnected).Select(connected => !connected);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

        var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
        DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnect);

        ShowMainCommand = ReactiveCommand.Create(ShowMain);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings);

        trayIconManager ??= new TrayIconManager(this);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

    private UserControl currentPage = new MainPage();
    public UserControl CurrentPage
    {
        set => this.RaiseAndSetIfChanged(ref currentPage, value);
    }

    public ReactiveCommand<Unit, Unit> ShowMainCommand;
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand;

    public string IpAddress
    {
        get
        {
            return ipAddress;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref ipAddress, value);
            SaveConfig();
        }
    }

    public string ClientId
    {
        get
        {
            return clientId;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref clientId, value);
            SaveConfig();
        }
    }

    public string BigImageKey
    {
        get
        {
            return bigImageKey;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref bigImageKey, value);
            SaveConfig();
        }
    }

    public string BigImageText
    {
        get
        {
            return bigImageText;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref bigImageText, value);
            SaveConfig();
        }
    }

    public string SmallImageKey
    {
        get
        {
            return smallImageKey;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref smallImageKey, value);
            SaveConfig();
        }
    }

    public string StateText
    {
        get
        {
            return stateText;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref stateText, value);
            SaveConfig();
        }
    }

    public bool ShowTimeLapsed
    {
        get
        {
            return showTimeLapsed;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref showTimeLapsed, value);
            SaveConfig();
        }
    }

    public bool MinimizeToTray
    {
        get
        {
            return minimizeToTray;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref minimizeToTray, value);
            SaveConfig();
        }
    }

    public bool DisplayHomeMenu
    {
        get
        {
            return displayHomeMenu;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref displayHomeMenu, value);
            SaveConfig();
        }
    }

    public bool AutoConvertIpToMac
    {
        get
        {
            return autoConvertIpToMac;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref autoConvertIpToMac, value);
            SaveConfig();
        }
    }

    public string Status
    {
        get
        {
            return status;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref status, value);
            SaveConfig();
        }
    }

    public bool IsConnected
    {
        get
        {
            return isConnected;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref isConnected, value);
            SaveConfig();
        }
    }

    public event EventHandler? ShowMainWindowRequested;

    public void ShowMainWindow()
    {
        ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleConnection()
    {
        if (IsConnected)
        {
            Disconnect();
        }
        else
        {
            ConnectAsync().FireAndForget();
        }
    }

    private async Task UpdateConnectionStatusAsync(bool isConnected)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsConnected = isConnected;
            trayIconManager?.UpdateIcon(isConnected);
        });
    }

    public void ExitApplication()
    {
        SaveConfig();
        Disconnect();
        Environment.Exit(0);
    }

    public void Dispose()
    {
        if (trayIconManager == null) return;
        trayIconManager.Dispose();
        trayIconManager = null;
    }

    private void ShowMain()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPage = new MainPage { DataContext = this };
        });
    }

    private void ShowSettings()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPage = new SettingsPage { DataContext = this };
        });
    }
    private async Task ConnectAsync()
    {
        if (isConnected)
        {
            Disconnect();
            return;
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            Status = "Client ID cannot be empty";
            return;
        }

        // Check and see if we have an IP
        if (IPAddress.TryParse(ipAddress, out resolvedIpAddress))
        {
            if (!hasSeenMacPrompt)
            {
                hasSeenMacPrompt = true;
                autoConvertIpToMac = true;
                await IpToMacAsync();
            }
            else if (autoConvertIpToMac)
            {
                await IpToMacAsync();
            }
        }
        else
        {
            // If in this block, means we don't have a valid IP.
            // Check and see if it's a MAC Address
            try
            {
                resolvedIpAddress = IPAddress.Parse(Utils.GetIpByMac(ipAddress));
            }
            catch (FormatException)
            {
                Status = "Invalid IP or MAC Address";
                return;
            }
        }

        cancellationTokenSource = new CancellationTokenSource();
        isConnected = true;

        try
        {
            await Task.Run(() => TryConnect(cancellationTokenSource.Token));
        }
        catch (OperationCanceledException)
        {
            Status = "Connection was cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Connection error: {ex.Message}";
            isConnected = false;
        }
    }

    private void Disconnect()
    {
        cancellationTokenSource?.Cancel();
        rpc?.Dispose();
        rpc = null;
        client?.Close();
        client = null;
        isConnected = false;
        Status = "Disconnected";
        lastProgramName = string.Empty;
        time = null;
        UpdateConnectionStatusAsync(false).FireAndForget();
    }

    private async Task TryConnect(CancellationToken cancellationToken)
    {
        rpc = new DiscordRpcClient(clientId);
        rpc.Initialize();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                client = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = 5500, SendTimeout = 5500
                };

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Status = "Attempting to connect to server...";
                });

                var localEndPoint = new IPEndPoint(resolvedIpAddress!, 0xCAFE);

                await client.ConnectAsync(localEndPoint, cancellationToken);
                await Dispatcher.UIThread.InvokeAsync(() => Status = "Connected to the server!");
                await UpdateConnectionStatusAsync(true);

                await DataListenAsync(cancellationToken);
            }
            catch (ArgumentNullException)
            {
                await Task.Delay(1000, cancellationToken);
                resolvedIpAddress = IPAddress.Parse(Utils.GetIpByMac(ipAddress));
            }
            catch (SocketException)
            {
                client?.Close();
                if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                await Task.Delay(5000, cancellationToken);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => Status = $"Connection error: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task DataListenAsync(CancellationToken cancellationToken)
    {
        manualUpdate = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytes = await PresenceCommon.Utils.ReceiveExactlyAsync(client!,
                    cancellationToken: cancellationToken);
                await Dispatcher.UIThread.InvokeAsync(() => Status = "Connected to the server!");

                var title = new Title(bytes);
                if (title.Magic == 0xffaadd23)
                {
                    if (lastProgramName != title.Name)
                    {
                        time = Timestamps.Now;
                    }

                    if (lastProgramName != title.Name || manualUpdate)
                    {
                        await UpdatePropertiesFromTitle(title);

                        if (rpc != null)
                        {
                            if (!DisplayHomeMenu && title.Name == "Home Menu")
                                rpc.ClearPresence();
                            else
                            {
                                rpc.SetPresence(PresenceCommon.Utils.CreateDiscordPresence(title, time, BigImageKey,
                                    BigImageText, SmallImageKey, StateText, ShowTimeLapsed));
                            }
                        }

                        manualUpdate = false;
                        lastProgramName = title.Name;
                    }
                }
                else
                {
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                    client!.Close();
                    return;
                }
            }
            catch (SocketException)
            {
                if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                client!.Close();
                return;
            }
        }
    }

    private async Task UpdatePropertiesFromTitle(Title title)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            BigImageKey = $"0{title.ProgramId:x}";
            BigImageText = title.Name;
            StateText = $"Playing {title.Name}";
        });
    }

    private async Task IpToMacAsync()
    {
        var macAddress = Utils.GetMacByIp(resolvedIpAddress!.ToString());
        if (macAddress != string.Empty)
            ipAddress = macAddress;
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => Status = "Can't convert to MAC Address! Sorry!");
        }
    }

    private void LoadConfig()
    {
        if (!File.Exists("Config.json")) return;
        var cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"));
        if (cfg == null) return;
        showTimeLapsed = cfg.DisplayTimer;
        bigImageKey = cfg.BigKey;
        bigImageText = cfg.BigText;
        smallImageKey = cfg.SmallKey;
        ipAddress = cfg.Ip;
        stateText = cfg.State;
        clientId = cfg.Client;
        minimizeToTray = cfg.AllowTray;
        displayHomeMenu = cfg.DisplayMainMenu;
        hasSeenMacPrompt = cfg.SeenAutoMacPrompt;
        autoConvertIpToMac = cfg.AutoToMac;
    }

    private void SaveConfig()
    {
        var cfg = new Config
        {
            Ip = ipAddress,
            Client = clientId,
            BigKey = bigImageKey,
            SmallKey = smallImageKey,
            State = stateText,
            BigText = bigImageText,
            DisplayTimer = showTimeLapsed,
            AllowTray = minimizeToTray,
            DisplayMainMenu = displayHomeMenu,
            SeenAutoMacPrompt = hasSeenMacPrompt,
            AutoToMac = autoConvertIpToMac
        };
        File.WriteAllText("Config.json", JsonConvert.SerializeObject(cfg, Formatting.Indented));
    }
}

public class Config
{
    public string Ip { get; set; } = "";
    public string Client { get; set; } = "";
    public string BigKey { get; set; } = "";
    public string SmallKey { get; set; } = "";
    public string State { get; set; } = "";
    public string BigText { get; set; } = "";
    public bool DisplayTimer { get; set; }
    public bool AllowTray { get; set; }
    public bool DisplayMainMenu { get; set; }
    public bool SeenAutoMacPrompt { get; set; }
    public bool AutoToMac { get; set; }
}