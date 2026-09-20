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
        public long AcceptedDamageEvents { get; set; }
        public long RejectedDamageEvents { get; set; }
        public long ExtractedPlayers { get; set; }
        public long DeadPlayers { get; set; }
        public long SubmittedMatchResults { get; set; }
        public long DuplicateMatchResults { get; set; }
        public long FailedMatchResults { get; set; }
        public long AcceptedPickupAttempts { get; set; }
        public long RejectedPickupAttempts { get; set; }
        public long DuplicateLootPickups { get; set; }
        public long InventoryFullRejections { get; set; }
        public long AcceptedFireRequests { get; set; }
        public long RejectedFireRequests { get; set; }
        public long HitscanHits { get; set; }
        public long HitscanMisses { get; set; }
        public long GrenadesThrown { get; set; }
        public long GrenadesExploded { get; set; }
        public long ZoneDamageTicks { get; set; }
        public long MedItemsUsed { get; set; }
        public long MedItemsRejected { get; set; }

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
                 0d,
                 0,
                 0,
                 0,
                 0,
                 0,
                 0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0,
                  0)
        {
        }

        public GameServerStatus(
            string serverId,
            string state,
            ushort networkPort,
            string publicHost,
            ushort publicNetworkPort,
            DateTime startedAtUtc)
            : this(serverId, ParseState(state), networkPort, publicHost, publicNetworkPort, startedAtUtc)
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
            : this(
                serverId,
                state,
                networkPort,
                publicHost,
                publicNetworkPort,
                startedAtUtc,
                activeConnections,
                spawnedPlayers,
                acceptedAdmissions,
                rejectedAdmissions,
                invalidInputCommands,
                disconnects,
                serverTickRate,
                serverTickP95Ms,
                processMemoryMb,
                inboundKbps,
                outboundKbps,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0)
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
            double outboundKbps,
            long acceptedDamageEvents,
            long rejectedDamageEvents,
            long extractedPlayers,
            long deadPlayers,
            long submittedMatchResults,
            long duplicateMatchResults,
            long failedMatchResults)
            : this(
                serverId,
                state,
                networkPort,
                publicHost,
                publicNetworkPort,
                startedAtUtc,
                activeConnections,
                spawnedPlayers,
                acceptedAdmissions,
                rejectedAdmissions,
                invalidInputCommands,
                disconnects,
                serverTickRate,
                serverTickP95Ms,
                processMemoryMb,
                inboundKbps,
                outboundKbps,
                acceptedDamageEvents,
                rejectedDamageEvents,
                extractedPlayers,
                deadPlayers,
                submittedMatchResults,
                duplicateMatchResults,
                failedMatchResults,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0)
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
            double outboundKbps,
            long acceptedDamageEvents,
            long rejectedDamageEvents,
            long extractedPlayers,
            long deadPlayers,
            long submittedMatchResults,
            long duplicateMatchResults,
            long failedMatchResults,
            long acceptedPickupAttempts,
            long rejectedPickupAttempts,
            long duplicateLootPickups,
            long inventoryFullRejections,
            long acceptedFireRequests,
            long rejectedFireRequests,
            long hitscanHits,
            long hitscanMisses,
            long grenadesThrown,
            long grenadesExploded,
            long zoneDamageTicks,
            long medItemsUsed,
            long medItemsRejected)
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
            AcceptedDamageEvents = acceptedDamageEvents;
            RejectedDamageEvents = rejectedDamageEvents;
            ExtractedPlayers = extractedPlayers;
            DeadPlayers = deadPlayers;
            SubmittedMatchResults = submittedMatchResults;
            DuplicateMatchResults = duplicateMatchResults;
            FailedMatchResults = failedMatchResults;
            AcceptedPickupAttempts = acceptedPickupAttempts;
            RejectedPickupAttempts = rejectedPickupAttempts;
            DuplicateLootPickups = duplicateLootPickups;
            InventoryFullRejections = inventoryFullRejections;
            AcceptedFireRequests = acceptedFireRequests;
            RejectedFireRequests = rejectedFireRequests;
            HitscanHits = hitscanHits;
            HitscanMisses = hitscanMisses;
            GrenadesThrown = grenadesThrown;
            GrenadesExploded = grenadesExploded;
            ZoneDamageTicks = zoneDamageTicks;
            MedItemsUsed = medItemsUsed;
            MedItemsRejected = medItemsRejected;
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
                + $"\"outboundKbps\":{FormatDouble(OutboundKbps)},"
                + $"\"acceptedDamageEvents\":{AcceptedDamageEvents},"
                + $"\"rejectedDamageEvents\":{RejectedDamageEvents},"
                + $"\"extractedPlayers\":{ExtractedPlayers},"
                + $"\"deadPlayers\":{DeadPlayers},"
                + $"\"submittedMatchResults\":{SubmittedMatchResults},"
                + $"\"duplicateMatchResults\":{DuplicateMatchResults},"
                + $"\"failedMatchResults\":{FailedMatchResults},"
                + $"\"acceptedPickupAttempts\":{AcceptedPickupAttempts},"
                + $"\"rejectedPickupAttempts\":{RejectedPickupAttempts},"
                + $"\"duplicateLootPickups\":{DuplicateLootPickups},"
                + $"\"inventoryFullRejections\":{InventoryFullRejections},"
                + $"\"acceptedFireRequests\":{AcceptedFireRequests},"
                + $"\"rejectedFireRequests\":{RejectedFireRequests},"
                + $"\"hitscanHits\":{HitscanHits},"
                + $"\"hitscanMisses\":{HitscanMisses},"
                + $"\"grenadesThrown\":{GrenadesThrown},"
                + $"\"grenadesExploded\":{GrenadesExploded},"
                + $"\"zoneDamageTicks\":{ZoneDamageTicks},"
                + $"\"medItemsUsed\":{MedItemsUsed},"
                + $"\"medItemsRejected\":{MedItemsRejected}"
                + "}";
        }

        private static GameServerState ParseState(string state)
        {
            switch ((state ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "idle":
                    return GameServerState.Idle;
                case "reserved":
                    return GameServerState.Reserved;
                case "running":
                    return GameServerState.Running;
                default:
                    return GameServerState.Failed;
            }
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
