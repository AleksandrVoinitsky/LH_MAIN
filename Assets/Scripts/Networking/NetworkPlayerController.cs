using FishNet.Object;
using LH.Main.Unity.Gameplay;
using UnityEngine;

namespace LH.Main.Unity.Networking
{
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        private MovementState _movementState = MovementState.Initial;
        private bool _hasAcceptedMovementCommand;
        private double _lastAcceptedMovementCommandServerTime;

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
    }
}
