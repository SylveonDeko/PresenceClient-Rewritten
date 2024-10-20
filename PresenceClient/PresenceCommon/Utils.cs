using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using Newtonsoft.Json;
using PresenceCommon.Types;

namespace PresenceCommon;

public static class Utils
{
    private static readonly HttpClient Client = new();

    static Utils()
    {
        QuestOverrides = new Dictionary<string, OverrideInfo>();
        SwitchOverrides = new Dictionary<string, OverrideInfo>();
    }

    public static Dictionary<string, OverrideInfo> QuestOverrides { get; private set; }
    public static Dictionary<string, OverrideInfo> SwitchOverrides { get; private set; }

    public static async Task InitializeOverridesAsync()
    {
        try
        {
            var questJson = await Client.GetStringAsync(
                "https://raw.githubusercontent.com/Sun-Research-University/PresenceClient/master/Resource/QuestApplicationOverrides.json");
            QuestOverrides = JsonConvert.DeserializeObject<Dictionary<string, OverrideInfo>>(questJson);

            var switchJson = await Client.GetStringAsync(
                "https://raw.githubusercontent.com/Sun-Research-University/PresenceClient/master/Resource/SwitchApplicationOverrides.json");
            SwitchOverrides = JsonConvert.DeserializeObject<Dictionary<string, OverrideInfo>>(switchJson);
        }
        catch (Exception ex)
        {
            // Handle or log the exception as appropriate for your application
            Console.WriteLine($"Error initializing overrides: {ex.Message}");
        }
    }

    public static RichPresence CreateDiscordPresence(Title title, Timestamps time, string largeImageKey = "",
        string largeImageText = "", string smallImageKey = "", string state = "", bool useProvidedTime = true)
    {
        var presence = new RichPresence
        {
            State = state
        };

        var assets = new Assets
        {
            SmallImageKey = smallImageKey,
            LargeImageText = title.Name
        };

        if (title.ProgramId != 0xffaadd23)
        {
            assets.SmallImageText = "SwitchPresence-Rewritten";

            if (!SwitchOverrides.ContainsKey($"0{title.ProgramId:x}"))
            {
                assets.LargeImageKey = $"0{title.ProgramId:x}";
                presence.Details = $"Playing {title.Name}";
            }
            else
            {
                var pkgInfo = SwitchOverrides[$"0{title.ProgramId:x}"];
                assets.LargeImageKey = pkgInfo.CustomKey ?? $"0{title.ProgramId:x}";

                presence.Details = pkgInfo.CustomPrefix ?? "Playing";
                presence.Details += $" {title.Name}";
            }
        }
        else
        {
            assets.SmallImageText = "QuestPresence";

            if (!QuestOverrides.TryGetValue(title.Name, out var pkgInfo))
            {
                assets.LargeImageKey = title.Name.ToLower().Replace(" ", "");
                presence.Details = $"Playing {title.Name}";
            }
            else
            {
                assets.LargeImageKey = pkgInfo.CustomKey ?? title.Name.ToLower().Replace(" ", "");

                presence.Details = pkgInfo.CustomPrefix ?? "Playing";
                presence.Details += $" {title.Name}";
            }
        }

        if (!string.IsNullOrEmpty(largeImageKey))
            assets.LargeImageKey = largeImageKey;

        if (!string.IsNullOrEmpty(largeImageText))
            assets.LargeImageText = largeImageText;

        presence.Assets = assets;
        if (useProvidedTime)
            presence.Timestamps = time;

        return presence;
    }

    public static async Task<byte[]> ReceiveExactlyAsync(Socket handler, int length = 628,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[length];
        var receivedLength = 0;
        while (receivedLength < length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var receiveTask = Task.Factory.FromAsync(
                    (callback, state) => handler.BeginReceive(buffer, receivedLength, length - receivedLength,
                        SocketFlags.None, callback, state),
                    handler.EndReceive,
                    null);

                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(-1, cancellationToken));

                if (completedTask == receiveTask)
                {
                    var nextLength = await receiveTask;
                    if (nextLength == 0) throw new SocketException((int)SocketError.ConnectionReset);
                    receivedLength += nextLength;
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new SocketException((int)SocketError.SocketError);
            }
        }

        return buffer;
    }

    public class OverrideInfo
    {
        public string CustomName { get; set; }
        public string CustomPrefix { get; set; }
        public string CustomKey { get; set; }
    }
}