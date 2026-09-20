using System;

namespace LH.Main.Unity.Gameplay
{
    public sealed class BodyZoneDamageTable
    {
        private readonly double _headMultiplier;
        private readonly double _torsoMultiplier;
        private readonly double _armsMultiplier;
        private readonly double _legsMultiplier;

        private BodyZoneDamageTable(double headMultiplier, double torsoMultiplier, double armsMultiplier, double legsMultiplier)
        {
            _headMultiplier = headMultiplier;
            _torsoMultiplier = torsoMultiplier;
            _armsMultiplier = armsMultiplier;
            _legsMultiplier = legsMultiplier;
        }

        public static BodyZoneDamageTable Standard()
        {
            return new BodyZoneDamageTable(2.0, 1.0, 0.75, 0.75);
        }

        public int CalculateDamage(int baseAmount, BodyZone bodyZone)
        {
            double multiplier;
            switch (bodyZone)
            {
                case BodyZone.Head:
                    multiplier = _headMultiplier;
                    break;
                case BodyZone.Torso:
                    multiplier = _torsoMultiplier;
                    break;
                case BodyZone.Arms:
                    multiplier = _armsMultiplier;
                    break;
                case BodyZone.Legs:
                    multiplier = _legsMultiplier;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bodyZone), bodyZone, null);
            }

            return Math.Max(1, (int)Math.Ceiling(baseAmount * multiplier));
        }
    }
}
