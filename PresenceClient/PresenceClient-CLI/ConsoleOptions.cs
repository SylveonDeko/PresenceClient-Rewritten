using System.Net;
using CommandLine;

namespace PresenceClient_CLI;

public class ConsoleOptions
{
    [Option('m', "ignore-home-screen", Required = false, Default = false, HelpText = "Don't display the home screen")]
    public bool IgnoreHomeScreen { get; set; }

    [Value(0, MetaName = "IP", Required = true, HelpText = "The IP address of your device")]
    public string Ip { get; set; }
    public IPAddress ParsedIp { get; set; }

    [Value(1, MetaName = "Client ID", Required = true, HelpText = "The Client ID of your Discord Rich Presence application")]
    public ulong ClientId { get; set; }
}