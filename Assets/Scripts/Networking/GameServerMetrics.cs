using System;
using System.Threading;

namespace LH.Main.Unity.Networking
{
    public static class GameServerMetrics
    {
        private const int MaxTickSamples = 256;
        private static readonly object TickSampleLock = new object();
        private static readonly object TrafficLock = new object();
        private static readonly double[] TickSamplesMs = new double[MaxTickSamples];
        private static int _tickSampleCount;
        private static int _nextTickSampleIndex;
        private static int _activeConnections;
        private static int _spawnedPlayers;
        private static int _serverTickRate;
        private static double _inboundKbps;
        private static double _outboundKbps;
        private static long _acceptedAdmissions;
        private static long _rejectedAdmissions;
        private static long _invalidInputCommands;
        private static long _disconnects;
        private static long _acceptedDamageEvents;
        private static long _rejectedDamageEvents;
        private static long _extractedPlayers;
        private static long _deadPlayers;
        private static long _submittedMatchResults;
        private static long _duplicateMatchResults;
        private static long _failedMatchResults;
        private static long _acceptedPickupAttempts;
        private static long _rejectedPickupAttempts;
        private static long _duplicateLootPickups;
        private static long _inventoryFullRejections;
        private static long _acceptedFireRequests;
        private static long _rejectedFireRequests;
        private static long _hitscanHits;
        private static long _hitscanMisses;
        private static long _grenadesThrown;
        private static long _grenadesExploded;
        private static long _zoneDamageTicks;
        private static long _medItemsUsed;
        private static long _medItemsRejected;

        public static void RecordAdmissionAccepted()
        {
            Interlocked.Increment(ref _acceptedAdmissions);
        }

        public static void RecordAdmissionRejected(string reason)
        {
            Interlocked.Increment(ref _rejectedAdmissions);
        }

        public static void RecordInvalidInput(string reason)
        {
            Interlocked.Increment(ref _invalidInputCommands);
        }

        public static void RecordDisconnect()
        {
            Interlocked.Increment(ref _disconnects);
        }

        public static void RecordDamageAccepted()
        {
            Interlocked.Increment(ref _acceptedDamageEvents);
        }

        public static void RecordDamageRejected(string reason)
        {
            Interlocked.Increment(ref _rejectedDamageEvents);
        }

        public static void SetTerminalPlayerCounts(int extractedPlayers, int deadPlayers)
        {
            Interlocked.Exchange(ref _extractedPlayers, Math.Max(0, extractedPlayers));
            Interlocked.Exchange(ref _deadPlayers, Math.Max(0, deadPlayers));
        }

        public static void RecordPlayerExtracted()
        {
            Interlocked.Increment(ref _extractedPlayers);
        }

        public static void RecordPlayerDead()
        {
            Interlocked.Increment(ref _deadPlayers);
        }

        public static void RecordMatchResultSubmitted(bool duplicate)
        {
            Interlocked.Increment(ref _submittedMatchResults);
            if (duplicate)
                Interlocked.Increment(ref _duplicateMatchResults);
        }

        public static void RecordMatchResultFailed()
        {
            Interlocked.Increment(ref _failedMatchResults);
        }

        public static void RecordPickupAccepted()
        {
            Interlocked.Increment(ref _acceptedPickupAttempts);
        }

        public static void RecordPickupRejected(string reason)
        {
            Interlocked.Increment(ref _rejectedPickupAttempts);
        }

        public static void RecordDuplicateLootPickup()
        {
            Interlocked.Increment(ref _duplicateLootPickups);
        }

        public static void RecordInventoryFullRejection()
        {
            Interlocked.Increment(ref _rejectedPickupAttempts);
            Interlocked.Increment(ref _inventoryFullRejections);
        }

        public static void RecordFireAccepted(bool hit)
        {
            Interlocked.Increment(ref _acceptedFireRequests);
            if (hit)
                Interlocked.Increment(ref _hitscanHits);
            else
                Interlocked.Increment(ref _hitscanMisses);
        }

        public static void RecordFireRejected(string reason)
        {
            Interlocked.Increment(ref _rejectedFireRequests);
        }

        public static void RecordGrenadeThrown()
        {
            Interlocked.Increment(ref _grenadesThrown);
        }

        public static void RecordGrenadeExploded()
        {
            Interlocked.Increment(ref _grenadesExploded);
        }

        public static void RecordZoneDamageTick()
        {
            Interlocked.Increment(ref _zoneDamageTicks);
        }

        public static void RecordMedItemUsed()
        {
            Interlocked.Increment(ref _medItemsUsed);
        }

        public static void RecordMedItemRejected(string reason)
        {
            Interlocked.Increment(ref _medItemsRejected);
        }

        public static void SetConnectionCounts(int activeConnections, int spawnedPlayers)
        {
            Volatile.Write(ref _activeConnections, Math.Max(0, activeConnections));
            Volatile.Write(ref _spawnedPlayers, Math.Max(0, spawnedPlayers));
        }

        public static void SetActiveConnectionCount(int activeConnections)
        {
            Volatile.Write(ref _activeConnections, Math.Max(0, activeConnections));
        }

        public static void SetSpawnedPlayerCount(int spawnedPlayers)
        {
            Volatile.Write(ref _spawnedPlayers, Math.Max(0, spawnedPlayers));
        }

        public static void SetTrafficKbps(double inboundKbps, double outboundKbps)
        {
            lock (TrafficLock)
            {
                _inboundKbps = SanitizeMetricDouble(inboundKbps);
                _outboundKbps = SanitizeMetricDouble(outboundKbps);
            }
        }

        public static void SetServerTickRate(int serverTickRate)
        {
            Volatile.Write(ref _serverTickRate, Math.Max(0, serverTickRate));
        }

        public static void RecordServerTickSample(double elapsedMs)
        {
            if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs < 0d)
                return;

            lock (TickSampleLock)
            {
                TickSamplesMs[_nextTickSampleIndex] = elapsedMs;
                _nextTickSampleIndex = (_nextTickSampleIndex + 1) % MaxTickSamples;
                if (_tickSampleCount < MaxTickSamples)
                    _tickSampleCount++;
            }
        }

        public static Snapshot GetSnapshot()
        {
            double inboundKbps;
            double outboundKbps;
            lock (TrafficLock)
            {
                inboundKbps = _inboundKbps;
                outboundKbps = _outboundKbps;
            }

            return new Snapshot(
                Volatile.Read(ref _activeConnections),
                Volatile.Read(ref _spawnedPlayers),
                Interlocked.Read(ref _acceptedAdmissions),
                Interlocked.Read(ref _rejectedAdmissions),
                Interlocked.Read(ref _invalidInputCommands),
                Interlocked.Read(ref _disconnects),
                Volatile.Read(ref _serverTickRate),
                GetTickP95Ms(),
                GC.GetTotalMemory(false) / (1024 * 1024),
                inboundKbps,
                outboundKbps,
                Interlocked.Read(ref _acceptedDamageEvents),
                Interlocked.Read(ref _rejectedDamageEvents),
                Interlocked.Read(ref _extractedPlayers),
                Interlocked.Read(ref _deadPlayers),
                Interlocked.Read(ref _submittedMatchResults),
                Interlocked.Read(ref _duplicateMatchResults),
                Interlocked.Read(ref _failedMatchResults),
                Interlocked.Read(ref _acceptedPickupAttempts),
                Interlocked.Read(ref _rejectedPickupAttempts),
                Interlocked.Read(ref _duplicateLootPickups),
                Interlocked.Read(ref _inventoryFullRejections),
                Interlocked.Read(ref _acceptedFireRequests),
                Interlocked.Read(ref _rejectedFireRequests),
                Interlocked.Read(ref _hitscanHits),
                Interlocked.Read(ref _hitscanMisses),
                Interlocked.Read(ref _grenadesThrown),
                Interlocked.Read(ref _grenadesExploded),
                Interlocked.Read(ref _zoneDamageTicks),
                Interlocked.Read(ref _medItemsUsed),
                Interlocked.Read(ref _medItemsRejected));
        }

        private static double SanitizeMetricDouble(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
        }

        private static double GetTickP95Ms()
        {
            lock (TickSampleLock)
            {
                if (_tickSampleCount == 0)
                    return 0d;

                double[] samples = new double[_tickSampleCount];
                Array.Copy(TickSamplesMs, samples, _tickSampleCount);
                Array.Sort(samples);
                int index = Math.Min(samples.Length - 1, (int)Math.Ceiling(samples.Length * 0.95d) - 1);
                return samples[index];
            }
        }

        public readonly struct Snapshot
        {
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
            public long AcceptedDamageEvents { get; }
            public long RejectedDamageEvents { get; }
            public long ExtractedPlayers { get; }
            public long DeadPlayers { get; }
            public long SubmittedMatchResults { get; }
            public long DuplicateMatchResults { get; }
            public long FailedMatchResults { get; }
            public long AcceptedPickupAttempts { get; }
            public long RejectedPickupAttempts { get; }
            public long DuplicateLootPickups { get; }
            public long InventoryFullRejections { get; }
            public long AcceptedFireRequests { get; }
            public long RejectedFireRequests { get; }
            public long HitscanHits { get; }
            public long HitscanMisses { get; }
            public long GrenadesThrown { get; }
            public long GrenadesExploded { get; }
            public long ZoneDamageTicks { get; }
            public long MedItemsUsed { get; }
            public long MedItemsRejected { get; }

            public Snapshot(
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
        }
    }
}
