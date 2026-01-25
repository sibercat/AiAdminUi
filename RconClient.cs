using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AiAdminUi;

public sealed partial class RconClient
{
    private const byte PACKET_AUTH = 0x01;
    private const byte PACKET_EXEC = 0x02;

    private const byte CMD_GETPLAYERDATA = 0x77;

    private readonly string _host;
    private readonly int _port;
    private readonly string _password;

    public RconClient(string host, int port, string password)
    {
        _host = host;
        _port = port;
        _password = password;
    }

    [GeneratedRegex(@"Name:\s*([^,]+),\s*PlayerID:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerDataRegex();

    public async Task<List<RconPlayer>> GetPlayerListAsync()
    {
        var players = new List<RconPlayer>();
        var response = await SendCommandAsync(CMD_GETPLAYERDATA, "");

        if (string.IsNullOrWhiteSpace(response))
        {
            return players;
        }

        var matches = PlayerDataRegex().Matches(response);
        foreach (Match match in matches)
        {
            players.Add(new RconPlayer(
                match.Groups[1].Value.Trim(),
                match.Groups[2].Value.Trim()));
        }

        return players;
    }

    private async Task<string?> SendCommandAsync(byte commandType, string args)
    {
        using (var client = new TcpClient())
        {
            // Modern .NET 10 timeout handling
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(_host, _port, cts.Token);
            
            if (!client.Connected) return null;

            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 5000;
                stream.WriteTimeout = 5000;

                if (!await AuthenticateAsync(stream))
                {
                    return null;
                }

                var payload = new List<byte> { PACKET_EXEC, commandType };
                if (!string.IsNullOrEmpty(args))
                {
                    payload.AddRange(Encoding.UTF8.GetBytes(args));
                }
                payload.Add(0x00);

                await stream.WriteAsync(payload.ToArray(), 0, payload.Count);
                return await ReadResponseAsync(stream);
            }
        }
    }

    private async Task<bool> AuthenticateAsync(NetworkStream stream)
    {
        var payload = new List<byte> { PACKET_AUTH };
        payload.AddRange(Encoding.UTF8.GetBytes(_password));
        payload.Add(0x00);

        await stream.WriteAsync(payload.ToArray(), 0, payload.Count);
        string response = await ReadResponseAsync(stream);
        return response.Contains("Accepted", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("Logged in", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadResponseAsync(NetworkStream stream)
    {
        var buffer = new byte[8192];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        if (bytesRead == 0) return string.Empty;
        return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
    }
}

public sealed class RconPlayer
{
    public string PlayerName { get; }
    public string PlayerId { get; }

    public RconPlayer(string playerName, string playerId)
    {
        PlayerName = playerName;
        PlayerId = playerId;
    }
}
