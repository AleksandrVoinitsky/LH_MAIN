using System;

namespace LH.Main.Unity.Networking
{
    public readonly struct MovementState
    {
        public static MovementState Initial => new MovementState(0f, 0f, 0f, 0f, 0u, 0d, false);
        public static MovementState InitialAt(float positionX, float positionY) => new MovementState(positionX, positionY, 0f, 0f, 0u, 0d, false);

        public float PositionX { get; }
        public float PositionY { get; }
        public float VelocityX { get; }
        public float VelocityY { get; }
        public uint LastSequence { get; }
        public double LastClientTime { get; }
        public bool HasReceivedCommand { get; }

        public MovementState(float positionX, float positionY, float velocityX, float velocityY, uint lastSequence, double lastClientTime)
            : this(positionX, positionY, velocityX, velocityY, lastSequence, lastClientTime, true)
        {
        }

        private MovementState(
            float positionX,
            float positionY,
            float velocityX,
            float velocityY,
            uint lastSequence,
            double lastClientTime,
            bool hasReceivedCommand)
        {
            PositionX = positionX;
            PositionY = positionY;
            VelocityX = velocityX;
            VelocityY = velocityY;
            LastSequence = lastSequence;
            LastClientTime = lastClientTime;
            HasReceivedCommand = hasReceivedCommand;
        }

        public MovementState WithLastSequence(uint sequence)
        {
            return new MovementState(PositionX, PositionY, VelocityX, VelocityY, sequence, LastClientTime, true);
        }

        public MovementState WithTiming(uint sequence, double clientTime)
        {
            return new MovementState(PositionX, PositionY, VelocityX, VelocityY, sequence, clientTime, true);
        }

        internal MovementState WithMotion(float positionX, float positionY, float velocityX, float velocityY, MovementCommand command)
        {
            return new MovementState(positionX, positionY, velocityX, velocityY, command.Sequence, command.SentAtClientTime, true);
        }
    }

    public readonly struct MovementValidationResult
    {
        public bool Accepted { get; }
        public string Reason { get; }
        public MovementState NextState { get; }

        private MovementValidationResult(bool accepted, string reason, MovementState nextState)
        {
            Accepted = accepted;
            Reason = reason;
            NextState = nextState;
        }

        public static MovementValidationResult Accept(MovementState nextState)
        {
            return new MovementValidationResult(true, string.Empty, nextState);
        }

        public static MovementValidationResult Reject(string reason, MovementState currentState)
        {
            return new MovementValidationResult(false, reason, currentState);
        }
    }

    public static class MovementValidator
    {
        public const float MaxSpeed = 6f;
        public const float MaxAcceleration = 24f;
        public const float MaxCommandsPerSecond = 30f;

        private const float AxisLimit = 1f;

        public static MovementValidationResult Validate(MovementCommand command, MovementState state, float deltaTime)
        {
            if (!IsFinite(command.MoveX) || !IsFinite(command.MoveY) || double.IsNaN(command.SentAtClientTime) || double.IsInfinity(command.SentAtClientTime))
                return MovementValidationResult.Reject("axis_non_finite", state);

            if (Math.Abs(command.MoveX) > AxisLimit || Math.Abs(command.MoveY) > AxisLimit)
                return MovementValidationResult.Reject("axis_out_of_range", state);

            if (state.HasReceivedCommand && command.Sequence <= state.LastSequence)
                return MovementValidationResult.Reject("sequence_not_newer", state);

            float safeDeltaTime = Math.Max(0f, deltaTime);
            float minimumCommandSpacing = 1f / MaxCommandsPerSecond;
            if (state.HasReceivedCommand && safeDeltaTime < minimumCommandSpacing)
                return MovementValidationResult.Reject("command_rate_exceeded", state);

            float inputX = command.MoveX;
            float inputY = command.MoveY;
            float inputMagnitude = Magnitude(inputX, inputY);
            if (inputMagnitude > 1f)
            {
                inputX /= inputMagnitude;
                inputY /= inputMagnitude;
            }

            float desiredVelocityX = inputX * MaxSpeed;
            float desiredVelocityY = inputY * MaxSpeed;
            float velocityX = state.VelocityX;
            float velocityY = state.VelocityY;
            ClampMagnitude(ref velocityX, ref velocityY, MaxSpeed);

            float deltaVelocityX = desiredVelocityX - velocityX;
            float deltaVelocityY = desiredVelocityY - velocityY;
            ClampMagnitude(ref deltaVelocityX, ref deltaVelocityY, MaxAcceleration * safeDeltaTime);

            velocityX += deltaVelocityX;
            velocityY += deltaVelocityY;
            ClampMagnitude(ref velocityX, ref velocityY, MaxSpeed);

            float positionX = state.PositionX + velocityX * safeDeltaTime;
            float positionY = state.PositionY + velocityY * safeDeltaTime;

            return MovementValidationResult.Accept(state.WithMotion(positionX, positionY, velocityX, velocityY, command));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Magnitude(float x, float y)
        {
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static void ClampMagnitude(ref float x, ref float y, float maxMagnitude)
        {
            float magnitude = Magnitude(x, y);
            if (magnitude <= maxMagnitude || magnitude <= 0f)
                return;

            float scale = maxMagnitude / magnitude;
            x *= scale;
            y *= scale;
        }
    }
}
