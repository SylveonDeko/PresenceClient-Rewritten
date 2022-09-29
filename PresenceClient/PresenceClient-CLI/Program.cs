using System;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using CommandLine;
using DiscordRPC;
using PresenceCommon;
using PresenceCommon.Types;

namespace PresenceClient_CLI;

internal class Program
{
    private static Timer _timer;
    private static Socket _client;
    private static string _lastProgramName = string.Empty;
    private static Timestamps _time;
    private static DiscordRpcClient _rpc;
    private static ConsoleOptions _arguments;

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        Parser.Default.ParseArguments<ConsoleOptions>(args)
            .WithParsed(arguments =>
            {
                if (!IPAddress.TryParse(arguments.Ip, out var iPAddress))
                {
                    Console.WriteLine("Invalid IP");
                    Environment.Exit(1);
                }
                arguments.ParsedIp = iPAddress;
                _rpc = new DiscordRpcClient(arguments.ClientId.ToString());
                _arguments = arguments;
            })
            .WithNotParsed(_ => Environment.Exit(1));

        if (!_rpc.Initialize())
        {
            Console.WriteLine("Unable to start RPC!");
            return 2;
        }

        var localEndPoint = new IPEndPoint(_arguments.ParsedIp, 0xCAFE);

        _timer = new Timer
        {
            Interval = 15000,
            Enabled = false,
        };
        _timer.Elapsed += OnConnectTimeout;

        while (true)
        {
            _client = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = 5500,
                SendTimeout = 5500
            };

            _timer.Enabled = true;

            try
            {
                var result = _client.BeginConnect(localEndPoint, null, null);
                var success = result.AsyncWaitHandle.WaitOne(2000, true);
                if (!success)
                {
                    //UpdateStatus("Could not connect to Server! Retrying...", Color.DarkRed);
                    _client.Close();
                }
                else
                {
                    _client.EndConnect(result);
                    _timer.Enabled = false;

                    DataListen();
                }
            }
            catch (SocketException)
            {
                _client.Close();
                if (_rpc != null && !_rpc.IsDisposed) _rpc.ClearPresence();
            }
        }
    }

    private static void DataListen()
    {
        while (true)
        {
            try
            {
                var bytes = Utils.ReceiveExactly(_client);

                var title = new Title(bytes);
                if (title.Magic == 0xffaadd23)
                {
                    if (_lastProgramName != title.Name)
                    {
                        _time = Timestamps.Now;
                    }

                    if (_rpc is not { CurrentPresence: null } && _lastProgramName == title.Name) continue;
                    if (_arguments.IgnoreHomeScreen && title.Name == "Home Menu")
                    {
                        _rpc.ClearPresence();
                    }
                    else
                    {
                        _rpc.SetPresence(Utils.CreateDiscordPresence(title, _time));
                    }
                    _lastProgramName = title.Name;
                }
                else
                {
                    if (_rpc != null && !_rpc.IsDisposed) _rpc.ClearPresence();
                    _client.Close();
                    return;
                }
            }
            catch (SocketException)
            {
                if (_rpc != null && !_rpc.IsDisposed) _rpc.ClearPresence();
                _client.Close();
                return;
            }
        }
    }

    private static void OnConnectTimeout(object sender, ElapsedEventArgs e)
    {
        _lastProgramName = string.Empty;
        _time = null;
    }

    private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        if (_client != null && _client.Connected)
            _client.Close();

        if(_rpc != null && !_rpc.IsDisposed)
            _rpc.Dispose();
    }
}