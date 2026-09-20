using System;
using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public sealed class BodyZoneHitbox : MonoBehaviour
    {
        [SerializeField]
        private string _playerId = string.Empty;

        [SerializeField]
        private BodyZone _zone = BodyZone.Torso;

        public string PlayerId => _playerId;
        public BodyZone Zone => _zone;

        public void ConfigureForTest(Guid playerId, BodyZone zone)
        {
            _playerId = playerId.ToString();
            _zone = zone;
        }
    }
}
