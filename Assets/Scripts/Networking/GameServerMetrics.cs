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
                outboundKbps);
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
                double outboundKbps)
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
            }
        }
    }
}
