using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace LH.Main.Unity.Load
{
    public sealed class LoadScenarioReport
    {
        public DateTime StartedAtUtc { get; }
        public int DurationSeconds { get; }
        public int TargetClients { get; }
        public int ConnectedClients { get; set; }
        public int SpawnedClients { get; set; }
        public int CompletedClients { get; set; }
        public int FailedClients { get; set; }
        public List<string> ServerStatusSnapshots { get; } = new List<string>();
        public List<string> DisconnectReasons { get; } = new List<string>();
        public List<string> MachineNotes { get; } = new List<string>();
        public List<string> MetricCollectionGaps { get; } = new List<string>();

        public LoadScenarioReport(DateTime startedAtUtc, int durationSeconds, int targetClients, int connectedClients = 0, int spawnedClients = 0, int completedClients = 0, int failedClients = 0)
        {
            StartedAtUtc = startedAtUtc.Kind == DateTimeKind.Utc ? startedAtUtc : startedAtUtc.ToUniversalTime();
            DurationSeconds = durationSeconds;
            TargetClients = targetClients;
            ConnectedClients = connectedClients;
            SpawnedClients = spawnedClients;
            CompletedClients = completedClients;
            FailedClients = failedClients;
        }

        public void WriteJson(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, ToJson());
        }

        public void RecordFailedClients(string reason, string machineNote = null)
        {
            FailedClients = Math.Max(FailedClients, TargetClients - CompletedClients);
            if (!string.IsNullOrWhiteSpace(reason))
                DisconnectReasons.Add(reason);
            if (!string.IsNullOrWhiteSpace(machineNote))
                MachineNotes.Add(machineNote);
        }

        public string ToJson()
        {
            return "{"
                + $"\"startedAtUtc\":\"{StartedAtUtc.ToString("O", CultureInfo.InvariantCulture)}\","
                + $"\"durationSeconds\":{DurationSeconds},"
                + $"\"targetClients\":{TargetClients},"
                + $"\"connectedClients\":{ConnectedClients},"
                + $"\"spawnedClients\":{SpawnedClients},"
                + $"\"completedClients\":{CompletedClients},"
                + $"\"failedClients\":{FailedClients},"
                + $"\"serverStatusSnapshots\":{RawJsonArray(ServerStatusSnapshots)},"
                + $"\"disconnectReasons\":{StringArray(DisconnectReasons)},"
                + $"\"machineNotes\":{StringArray(MachineNotes)},"
                + $"\"metricCollectionGaps\":{StringArray(MetricCollectionGaps)}"
                + "}";
        }

        private static string RawJsonArray(IReadOnlyList<string> values)
        {
            return "[" + string.Join(",", values) + "]";
        }

        private static string StringArray(IReadOnlyList<string> values)
        {
            var escaped = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                escaped[i] = "\"" + Escape(values[i]) + "\"";

            return "[" + string.Join(",", escaped) + "]";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }
    }
}
