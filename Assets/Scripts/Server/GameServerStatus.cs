using System;

namespace LH.Main.Unity.Server
{
    public sealed class GameServerStatus
    {
        public string ServerId { get; }
        public GameServerState State { get; }
        public ushort NetworkPort { get; }
        public string PublicHost { get; }
        public ushort PublicNetworkPort { get; }
        public DateTime StartedAtUtc { get; }

        public GameServerStatus(
            string serverId,
            GameServerState state,
            ushort networkPort,
            string publicHost,
            ushort publicNetworkPort,
            DateTime startedAtUtc)
        {
            ServerId = serverId;
            State = state;
            NetworkPort = networkPort;
            PublicHost = publicHost;
            PublicNetworkPort = publicNetworkPort;
            StartedAtUtc = startedAtUtc;
        }

        public string ToJson()
        {
            return "{"
                + $"\"serverId\":\"{Escape(ServerId)}\","
                + $"\"state\":\"{GameServerStateJson.ToJsonValue(State)}\","
                + $"\"networkPort\":{NetworkPort},"
                + $"\"publicHost\":\"{Escape(PublicHost)}\","
                + $"\"publicNetworkPort\":{PublicNetworkPort},"
                + $"\"startedAtUtc\":\"{StartedAtUtc:O}\""
                + "}";
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
