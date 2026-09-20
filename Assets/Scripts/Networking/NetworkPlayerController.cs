using FishNet.Object;
using LH.Main.Unity.Gameplay;
using LH.Main.Unity.Gameplay.Items;
using UnityEngine;

namespace LH.Main.Unity.Networking
{
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        private MovementState _movementState = MovementState.Initial;
        private bool _hasAcceptedMovementCommand;
        private double _lastAcceptedMovementCommandServerTime;
        private CoreMatchRuntime _coreMatchRuntime;
        private ServerPlayerRegistry _playerRegistry;
        private System.Guid _configuredPlayerId;
        private static CoreMatchRuntime s_serverRuntime;
        private static ServerPlayerRegistry s_serverPlayerRegistry;

        public MovementState AuthoritativeState => _movementState;
        public PlayerLifeState LifeState { get; private set; } = PlayerLifeState.Alive;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Vector3 position = transform.position;
            _movementState = MovementState.InitialAt(position.x, position.z);
            _hasAcceptedMovementCommand = false;
            _lastAcceptedMovementCommandServerTime = Time.realtimeSinceStartupAsDouble;
        }

        [ServerRpc]
        public void ServerApplyInput(MovementCommand command)
        {
            ApplyAuthoritativeInput(command);
        }

        [ServerRpc]
        public void ServerPickupLoot(System.Guid lootId, System.Guid transactionId)
        {
            InventoryTransactionResult result = ApplyServerPickup(ResolveRuntime(), lootId, transactionId);
            RecordPickupMetric(result);
        }

        [ServerRpc]
        public void ServerUseMed(string itemId, System.Guid transactionId)
        {
            InventoryTransactionResult result = ApplyServerUseMed(ResolveRuntime(), itemId, transactionId);
            if (result.Accepted)
                GameServerMetrics.RecordMedItemUsed();
            else
                GameServerMetrics.RecordMedItemRejected(result.Reason);
        }

        [ServerRpc]
        public void ServerFireWeapon(System.Guid requestId, Vector3 origin, Vector3 direction)
        {
            WeaponFireResult result = ApplyServerFire(ResolveRuntime(), requestId, origin, direction);
            if (result.Accepted)
                GameServerMetrics.RecordFireAccepted(hit: false);
            else
                GameServerMetrics.RecordFireRejected(result.Reason);
        }

        [ServerRpc]
        public void ServerReloadWeapon(System.Guid requestId)
        {
            CoreMatchRuntime runtime = ResolveRuntime();
            if (runtime == null || !TryResolvePlayerId(out System.Guid playerId))
                return;

            runtime.RegisterPlayer(playerId);
            runtime.TryReload(playerId, requestId, Time.realtimeSinceStartupAsDouble);
        }

        [ServerRpc]
        public void ServerThrowGrenade(System.Guid transactionId, Vector3 origin, Vector3 direction)
        {
            CoreMatchRuntime runtime = ResolveRuntime();
            if (runtime == null || !TryResolvePlayerId(out System.Guid playerId))
                return;

            runtime.RegisterPlayer(playerId);
            InventoryTransactionResult result = runtime.TryThrowGrenade(playerId, transactionId, origin, direction, Time.realtimeSinceStartupAsDouble);
            if (result.Accepted)
                GameServerMetrics.RecordGrenadeThrown();
        }

        public static void ConfigureServerCoreRuntime(CoreMatchRuntime runtime, ServerPlayerRegistry playerRegistry)
        {
            s_serverRuntime = runtime;
            s_serverPlayerRegistry = playerRegistry;
        }

        [Server]
        public void ConfigureCoreMatchRuntimeForTest(CoreMatchRuntime runtime, System.Guid playerId)
        {
            _coreMatchRuntime = runtime;
            _configuredPlayerId = playerId;
        }

        [Server]
        public InventoryTransactionResult ApplyServerPickupForTest(CoreMatchRuntime runtime, System.Guid lootId, System.Guid transactionId)
        {
            return ApplyServerPickup(runtime, lootId, transactionId);
        }

        [Server]
        public InventoryTransactionResult ApplyServerUseMedForTest(CoreMatchRuntime runtime, string itemId, System.Guid transactionId)
        {
            return ApplyServerUseMed(runtime, itemId, transactionId);
        }

        [Server]
        public WeaponFireResult ApplyServerFireForTest(CoreMatchRuntime runtime, System.Guid requestId, Vector3 origin, Vector3 direction)
        {
            return ApplyServerFire(runtime, requestId, origin, direction);
        }

        [Server]
        public void ApplyServerLifeState(PlayerLifeState lifeState)
        {
            if (LifeState != PlayerLifeState.Extracted && lifeState == PlayerLifeState.Extracted)
                GameServerMetrics.RecordPlayerExtracted();
            else if (LifeState != PlayerLifeState.Dead && lifeState == PlayerLifeState.Dead)
                GameServerMetrics.RecordPlayerDead();

            LifeState = lifeState;
        }

        public void ApplyAuthoritativeInput(MovementCommand command)
        {
            if (LifeState.IsTerminal())
            {
                GameServerMetrics.RecordInvalidInput("state_terminal");
                return;
            }

            double serverTime = Time.realtimeSinceStartupAsDouble;
            float deltaTime = _hasAcceptedMovementCommand
                ? (float)(serverTime - _lastAcceptedMovementCommandServerTime)
                : TimeManager != null ? (float)TimeManager.TickDelta : Time.deltaTime;
            MovementValidationResult result = MovementValidator.Validate(command, _movementState, deltaTime);
            if (!result.Accepted)
            {
                GameServerMetrics.RecordInvalidInput(result.Reason);
                return;
            }

            _movementState = result.NextState;
            _hasAcceptedMovementCommand = true;
            _lastAcceptedMovementCommandServerTime = serverTime;
            transform.position = new Vector3(_movementState.PositionX, transform.position.y, _movementState.PositionY);
        }

        private InventoryTransactionResult ApplyServerPickup(CoreMatchRuntime runtime, System.Guid lootId, System.Guid transactionId)
        {
            if (runtime == null || !TryResolvePlayerId(out System.Guid playerId))
                return InventoryTransactionResult.Rejected("player_not_registered");

            runtime.RegisterPlayer(playerId);
            return LifeState.IsTerminal()
                ? InventoryTransactionResult.Rejected("state_terminal")
                : runtime.TryPickup(playerId, lootId, transactionId, transform.position);
        }

        private InventoryTransactionResult ApplyServerUseMed(CoreMatchRuntime runtime, string itemId, System.Guid transactionId)
        {
            if (runtime == null || !TryResolvePlayerId(out System.Guid playerId))
                return InventoryTransactionResult.Rejected("player_not_registered");

            runtime.RegisterPlayer(playerId);
            return LifeState.IsTerminal()
                ? InventoryTransactionResult.Rejected("state_terminal")
                : runtime.TryUseMed(playerId, itemId, transactionId, Time.realtimeSinceStartupAsDouble);
        }

        private WeaponFireResult ApplyServerFire(CoreMatchRuntime runtime, System.Guid requestId, Vector3 origin, Vector3 direction)
        {
            if (runtime == null || !TryResolvePlayerId(out System.Guid playerId))
                return new WeaponFireResult(false, "player_not_registered", default);

            runtime.RegisterPlayer(playerId);
            return LifeState.IsTerminal()
                ? new WeaponFireResult(false, "state_terminal", default)
                : runtime.TryFire(playerId, new WeaponFireRequest(requestId), origin, direction, Time.realtimeSinceStartupAsDouble);
        }

        private CoreMatchRuntime ResolveRuntime()
        {
            return _coreMatchRuntime ?? s_serverRuntime;
        }

        private bool TryResolvePlayerId(out System.Guid playerId)
        {
            if (_configuredPlayerId != System.Guid.Empty)
            {
                playerId = _configuredPlayerId;
                return true;
            }

            ServerPlayerRegistry registry = _playerRegistry ?? s_serverPlayerRegistry;
            if (registry != null && Owner != null && registry.TryGetAcceptedPlayer(Owner.ClientId, out ServerPlayerRegistry.AcceptedPlayer acceptedPlayer))
            {
                playerId = acceptedPlayer.PlayerId;
                return true;
            }

            playerId = System.Guid.Empty;
            return false;
        }

        private static void RecordPickupMetric(InventoryTransactionResult result)
        {
            if (result.Accepted)
            {
                GameServerMetrics.RecordPickupAccepted();
                return;
            }

            if (result.Reason == "inventory_full")
                GameServerMetrics.RecordInventoryFullRejection();
            else
                GameServerMetrics.RecordPickupRejected(result.Reason);

            if (result.Reason == "transaction_duplicate" || result.Reason == "loot_not_found")
                GameServerMetrics.RecordDuplicateLootPickup();
        }
    }
}
