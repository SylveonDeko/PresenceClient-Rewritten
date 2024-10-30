using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DiscordRPC;
using PresenceClient.Helpers;
using PresenceClient.Platform;
using PresenceClient.Views;
using PresenceCommon.Types;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactiveUI;

namespace PresenceClient.ViewModels
{
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
        private UserControl currentPage;
        private readonly string _configPath;

        public MainWindowViewModel()
        {
            _configPath = PlatformHelper.GetConfigPath();
            currentPage = new MainPage();

            if (PlatformHelper.CanUseTrayIcon())
            {
                trayIconManager ??= new TrayIconManager(this);
            }

            LoadConfig();

            var canConnect = this.WhenAnyValue(x => x.IsConnected).Select(connected => !connected);
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);

            var canDisconnect = this.WhenAnyValue(x => x.IsConnected);
            DisconnectCommand = ReactiveCommand.Create(Disconnect, canDisconnect);

            ReactiveCommand.Create(ShowMain);
            ReactiveCommand.Create(ShowSettings);
        }

        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

        public UserControl CurrentPage
        {
            get => currentPage;
            set => this.RaiseAndSetIfChanged(ref currentPage, value);
        }

        public string IpAddress
        {
            get => ipAddress;
            set
            {
                this.RaiseAndSetIfChanged(ref ipAddress, value);
                SaveConfig();
            }
        }

        public string ClientId
        {
            get => clientId;
            set
            {
                this.RaiseAndSetIfChanged(ref clientId, value);
                SaveConfig();
            }
        }

        public string BigImageKey
        {
            get => bigImageKey;
            set
            {
                this.RaiseAndSetIfChanged(ref bigImageKey, value);
                SaveConfig();
            }
        }

        public string BigImageText
        {
            get => bigImageText;
            set
            {
                this.RaiseAndSetIfChanged(ref bigImageText, value);
                SaveConfig();
            }
        }

        public string SmallImageKey
        {
            get => smallImageKey;
            set
            {
                this.RaiseAndSetIfChanged(ref smallImageKey, value);
                SaveConfig();
            }
        }

        public string StateText
        {
            get => stateText;
            set
            {
                this.RaiseAndSetIfChanged(ref stateText, value);
                SaveConfig();
            }
        }

        public bool ShowTimeLapsed
        {
            get => showTimeLapsed;
            set
            {
                this.RaiseAndSetIfChanged(ref showTimeLapsed, value);
                SaveConfig();
            }
        }

        public bool MinimizeToTray
        {
            get => minimizeToTray;
            set
            {
                this.RaiseAndSetIfChanged(ref minimizeToTray, value);
                SaveConfig();
            }
        }

        public bool DisplayHomeMenu
        {
            get => displayHomeMenu;
            set
            {
                this.RaiseAndSetIfChanged(ref displayHomeMenu, value);
                SaveConfig();
            }
        }

        public bool AutoConvertIpToMac
        {
            get => autoConvertIpToMac;
            set
            {
                this.RaiseAndSetIfChanged(ref autoConvertIpToMac, value);
                SaveConfig();
            }
        }

        public string Status
        {
            get => status;
            set => this.RaiseAndSetIfChanged(ref status, value);
        }

        public bool IsConnected
        {
            get => isConnected;
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
                _ = ConnectAsync();
            }
        }

        private async Task UpdateConnectionStatusAsync(bool isConnectedSecond)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsConnected = isConnectedSecond;
                trayIconManager?.UpdateIcon(isConnectedSecond);
            });
        }

        public void ExitApplication()
        {
            Disconnect();
            Environment.Exit(0);
        }

        public void Dispose()
        {
            Disconnect();
            if (trayIconManager != null)
            {
                trayIconManager.Dispose();
                trayIconManager = null;
            }
        }

        private void ShowMain()
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = new MainPage
                {
                    DataContext = this
                };
            });
        }

        private void ShowSettings()
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = new SettingsPage
                {
                    DataContext = this
                };
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

            try
            {
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
                    var resolvedIp = NetworkUtils.GetIpByMac(ipAddress);
                    if (string.IsNullOrEmpty(resolvedIp))
                    {
                        Status = "Invalid IP or MAC Address";
                        return;
                    }

                    resolvedIpAddress = IPAddress.Parse(resolvedIp);
                }

                cancellationTokenSource = new CancellationTokenSource();
                isConnected = true;

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
            try
            {
                cancellationTokenSource?.Cancel();

                if (rpc is { IsDisposed: false })
                {
                    rpc.ClearPresence();
                    rpc.Dispose();
                }

                rpc = null;

                if (client != null)
                {
                    client.Close();
                    client.Dispose();
                }

                client = null;

                isConnected = false;
                Status = "Disconnected";
                lastProgramName = string.Empty;
                time = null;
                _ = UpdateConnectionStatusAsync(false);
            }
            catch (Exception ex)
            {
                Status = $"Error during disconnect: {ex.Message}";
            }
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
                    var newIp = NetworkUtils.GetIpByMac(ipAddress);
                    if (!string.IsNullOrEmpty(newIp))
                    {
                        resolvedIpAddress = IPAddress.Parse(newIp);
                    }
                }
                catch (SocketException)
                {
                    if (client != null)
                    {
                        client.Close();
                        client.Dispose();
                        client = null;
                    }

                    if (rpc is { IsDisposed: false }) rpc.ClearPresence();
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

                            if (rpc is { IsDisposed: false })
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
                        if (rpc is { IsDisposed: false }) rpc.ClearPresence();
                        if (client == null) return;
                        client.Close();
                        client.Dispose();
                        return;
                    }
                }
                catch (SocketException)
                {
                    if (rpc is { IsDisposed: false }) rpc.ClearPresence();
                    if (client == null) return;
                    client.Close();
                    client.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => Status = $"Error during data listen: {ex.Message}");
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
            if (resolvedIpAddress == null) return;

            var macAddress = NetworkUtils.GetMacByIp(resolvedIpAddress.ToString());
            if (!string.IsNullOrEmpty(macAddress))
            {
                ipAddress = macAddress;
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => Status = "Can't convert to MAC Address! Sorry!");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;

                var jsonString = File.ReadAllText(_configPath);
                var cfg = JsonSerializer.Deserialize(jsonString, SourceGenerationContext.Default.Config);
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
                trayIconManager?.EnableTrayIcon(cfg.AllowTray);
            }
            catch (Exception ex)
            {
                Status = $"Error loading config: {ex.Message}";
                minimizeToTray = false;
            }
        }


        private void SaveConfig()
        {
            try
            {
                PlatformHelper.EnsureConfigDirectory();

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

                var jsonString = JsonSerializer.Serialize(cfg, SourceGenerationContext.Default.Config);
                File.WriteAllText(_configPath, jsonString);
                trayIconManager?.EnableTrayIcon(minimizeToTray);
            }
            catch (Exception ex)
            {
                Status = $"Error saving config: {ex.Message}";
                Console.WriteLine("Error saving config: " + ex.Message);
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Config))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }

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

        public Config()
        {
            DisplayMainMenu = true;
            DisplayTimer = true;
        }
    }
}