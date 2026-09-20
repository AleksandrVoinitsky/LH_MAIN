using System;
using System.Globalization;

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
        public int ActiveConnections { get; }
        public int SpawnedPlayers { get; }
        public long AcceptedAdmissions { get; }
        public long RejectedAdmissions { get; }
        public long InvalidInputCommands { get; }
        public long Disconnects { get; }
        public int ServerTickRate { get; }
        public double ServerTickP95Ms { get; }
        public long ProcessMemoryMb { get; }
        public double InboundKbps { get; }
        public double OutboundKbps { get; }

        public GameServerStatus(
            string serverId,
            GameServerState state,
            ushort networkPort,
            string publicHost,
            ushort publicNetworkPort,
            DateTime startedAtUtc)
            : this(
                serverId,
                state,
                networkPort,
                publicHost,
                publicNetworkPort,
                startedAtUtc,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0d,
                GC.GetTotalMemory(false) / (1024 * 1024),
                0d,
                0d)
        {
        }

        public GameServerStatus(
            string serverId,
            GameServerState state,
            ushort networkPort,
            string publicHost,
            ushort publicNetworkPort,
            DateTime startedAtUtc,
            int activeConnections,
            int spawnedPlayers,
            long acceptedAdmissions,
            long rejectedAdmissions,
            long invalidInputCommands,
            long disconnects,
            int serverTickRate,
            double serverTickP95Ms,
            long processMemoryMb,
            double inboundKbps,
            double outboundKbps)
        {
            ServerId = serverId;
            State = state;
            NetworkPort = networkPort;
            PublicHost = publicHost;
            PublicNetworkPort = publicNetworkPort;
            StartedAtUtc = startedAtUtc;
            ActiveConnections = activeConnections;
            SpawnedPlayers = spawnedPlayers;
            AcceptedAdmissions = acceptedAdmissions;
            RejectedAdmissions = rejectedAdmissions;
            InvalidInputCommands = invalidInputCommands;
            Disconnects = disconnects;
            ServerTickRate = serverTickRate;
            ServerTickP95Ms = serverTickP95Ms;
            ProcessMemoryMb = processMemoryMb;
            InboundKbps = inboundKbps;
            OutboundKbps = outboundKbps;
        }

        public string ToJson()
        {
            return "{"
                + $"\"serverId\":\"{Escape(ServerId)}\","
                + $"\"state\":\"{GameServerStateJson.ToJsonValue(State)}\","
                + $"\"networkPort\":{NetworkPort},"
                + $"\"publicHost\":\"{Escape(PublicHost)}\","
                + $"\"publicNetworkPort\":{PublicNetworkPort},"
                + $"\"startedAtUtc\":\"{StartedAtUtc:O}\","
                + $"\"activeConnections\":{ActiveConnections},"
                + $"\"spawnedPlayers\":{SpawnedPlayers},"
                + $"\"acceptedAdmissions\":{AcceptedAdmissions},"
                + $"\"rejectedAdmissions\":{RejectedAdmissions},"
                + $"\"invalidInputCommands\":{InvalidInputCommands},"
                + $"\"disconnects\":{Disconnects},"
                + $"\"serverTickRate\":{ServerTickRate},"
                + $"\"serverTickP95Ms\":{FormatDouble(ServerTickP95Ms)},"
                + $"\"processMemoryMb\":{ProcessMemoryMb},"
                + $"\"inboundKbps\":{FormatDouble(InboundKbps)},"
                + $"\"outboundKbps\":{FormatDouble(OutboundKbps)}"
                + "}";
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
