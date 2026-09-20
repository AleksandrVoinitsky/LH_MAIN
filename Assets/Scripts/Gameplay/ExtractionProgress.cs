using System;

namespace LH.Main.Unity.Gameplay
{
    public sealed class ExtractionProgress
    {
        private readonly float _requiredHoldSeconds;
        private float _heldSeconds;

        public ExtractionProgress(float requiredHoldSeconds)
        {
            if (requiredHoldSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(requiredHoldSeconds));

            _requiredHoldSeconds = requiredHoldSeconds;
        }

        public bool Update(bool insideZone, float deltaSeconds)
        {
            if (!insideZone)
            {
                _heldSeconds = 0f;
                return false;
            }

            if (deltaSeconds > 0f)
                _heldSeconds += deltaSeconds;

            return _heldSeconds >= _requiredHoldSeconds;
        }
    }
}
