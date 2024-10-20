using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using DiscordRPC;
using PresenceCommon;
using PresenceCommon.Types;

namespace PresenceClient_CLI;

internal class Program
{
    private static Socket _client;
    private static string _lastProgramName = string.Empty;
    private static Timestamps _time;
    private static DiscordRpcClient _rpc;
    private static ConsoleOptions _arguments;
    private static CancellationTokenSource _cts;

    private static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        _cts = new CancellationTokenSource();

        var parseResult = Parser.Default.ParseArguments<ConsoleOptions>(args);
        if (parseResult.Tag == ParserResultType.NotParsed)
        {
            return 1;
        }

        _arguments = ((Parsed<ConsoleOptions>)parseResult).Value;
        if (!IPAddress.TryParse(_arguments.Ip, out var iPAddress))
        {
            Console.WriteLine("Invalid IP");
            return 1;
        }

        _arguments.ParsedIp = iPAddress;
        _rpc = new DiscordRpcClient(_arguments.ClientId.ToString());

        if (!_rpc.Initialize())
        {
            Console.WriteLine("Unable to start RPC!");
            return 2;
        }

        var localEndPoint = new IPEndPoint(_arguments.ParsedIp, 0xCAFE);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                using (_client = new Socket(SocketType.Stream, ProtocolType.Tcp))
                {
                    _client.ReceiveTimeout = 5500;
                    _client.SendTimeout = 5500;

                    await ConnectWithTimeoutAsync(_client, localEndPoint, TimeSpan.FromSeconds(2));
                    await DataListenAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation was requested
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (_rpc is { IsDisposed: false })
                {
                    _rpc.ClearPresence();
                }
                await Task.Delay(5000, _cts.Token);
            }
        }

        return 0;
    }

    private static async Task ConnectWithTimeoutAsync(Socket client, EndPoint endpoint, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _cts.Token);

        try
        {
            await client.ConnectAsync(endpoint, combinedCts.Token);
            Console.WriteLine("Connected to server.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Connection attempt timed out.");
        }
    }

    private static async Task DataListenAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var bytes = await Utils.ReceiveExactlyAsync(_client, cancellationToken: _cts.Token);

                var title = new Title(bytes);
                if (title.Magic == 0xffaadd23)
                {
                    if (_lastProgramName != title.Name)
                    {
                        _time = Timestamps.Now;
                    }

                    if (_rpc is not { CurrentPresence: null } && _lastProgramName == title.Name)
                    {
                        continue;
                    }

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
                    if (_rpc is { IsDisposed: false })
                    {
                        _rpc.ClearPresence();
                    }
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation was requested
                break;
            }
            catch (SocketException)
            {
                if (_rpc is { IsDisposed: false })
                {
                    _rpc.ClearPresence();
                }
                break;
            }
        }
    }

    private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        _cts.Cancel();
        _client?.Close();
        _rpc?.Dispose();
    }
}