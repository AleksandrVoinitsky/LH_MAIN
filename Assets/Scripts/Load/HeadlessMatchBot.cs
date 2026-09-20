using System;
using System.Threading;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Object;
using LH.Main.Unity.Client;
using LH.Main.Unity.Networking;
using UnityEngine;

namespace LH.Main.Unity.Load
{
    public readonly struct BotMove
    {
        public float MoveX { get; }
        public float MoveY { get; }

        public BotMove(float moveX, float moveY)
        {
            MoveX = moveX;
            MoveY = moveY;
        }
    }

    public sealed class BotScenario
    {
        public MatchAssignment Assignment { get; }
        public Guid PlayerId { get; }
        public int DurationSeconds { get; }
        public NetworkManager NetworkManager { get; }

        public BotScenario(MatchAssignment assignment, Guid playerId, int durationSeconds, NetworkManager networkManager)
        {
            Assignment = assignment;
            PlayerId = playerId;
            DurationSeconds = durationSeconds;
            NetworkManager = networkManager;
        }
    }

    public sealed class BotResult
    {
        public bool Connected { get; set; }
        public bool Spawned { get; set; }
        public bool Moved { get; set; }
        public bool DisconnectedCleanly { get; set; }
        public string FailureReason { get; set; } = string.Empty;
    }

    public sealed class HeadlessMatchBot
    {
        public const double SegmentSeconds = 10d;

        public static BotMove GetMoveForElapsedSeconds(double elapsedSeconds)
        {
            int segment = (int)Math.Floor(Math.Max(0d, elapsedSeconds) / SegmentSeconds) % 4;
            switch (segment)
            {
                case 0:
                    return new BotMove(0f, 1f);
                case 1:
                    return new BotMove(1f, 0f);
                case 2:
                    return new BotMove(0f, -1f);
                default:
                    return new BotMove(-1f, 0f);
            }
        }

        public async Task<BotResult> RunAsync(BotScenario scenario, CancellationToken cancellationToken)
        {
            var result = new BotResult();
            if (scenario == null || scenario.NetworkManager == null)
            {
                result.FailureReason = "network_manager_required";
                return result;
            }

            var client = new MatchConnectionClient(scenario.NetworkManager);
            result.Connected = await client.ConnectAsync(scenario.Assignment, scenario.PlayerId, cancellationToken);
            if (!result.Connected)
            {
                result.FailureReason = "connect_failed";
                return result;
            }

            NetworkPlayerController player = await WaitForOwnedPlayerAsync(scenario.NetworkManager, cancellationToken);
            result.Spawned = player != null;
            if (!result.Spawned)
            {
                result.FailureReason = "spawn_timeout";
                client.Disconnect();
                return result;
            }

            DateTime startedAtUtc = DateTime.UtcNow;
            uint sequence = 1;
            while ((DateTime.UtcNow - startedAtUtc).TotalSeconds < scenario.DurationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BotMove move = GetMoveForElapsedSeconds((DateTime.UtcNow - startedAtUtc).TotalSeconds);
                player.ServerApplyInput(new MovementCommand(sequence++, move.MoveX, move.MoveY, Time.realtimeSinceStartupAsDouble));
                result.Moved = true;
                await Task.Delay(100, cancellationToken);
            }

            result.DisconnectedCleanly = client.Disconnect();
            if (!result.DisconnectedCleanly)
                result.FailureReason = "disconnect_failed";

            return result;
        }

        private static async Task<NetworkPlayerController> WaitForOwnedPlayerAsync(NetworkManager networkManager, CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NetworkPlayerController[] players = UnityEngine.Object.FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);
                foreach (NetworkPlayerController player in players)
                {
                    NetworkObject networkObject = player.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.IsOwner)
                        return player;
                }

                await Task.Delay(100, cancellationToken);
            }

            return null;
        }
    }
}
