using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public sealed class ZoneVolume : MonoBehaviour
    {
        [SerializeField]
        private Collider _collider;

        private bool _loggedMissingCollider;

        private void Awake()
        {
            ResolveCollider();
        }

        private void Reset()
        {
            ResolveCollider();
        }

        public bool Contains(Vector3 worldPosition)
        {
            Collider zoneCollider = ResolveCollider();
            if (zoneCollider == null)
            {
                if (!_loggedMissingCollider)
                {
                    Debug.LogWarning("ZoneVolume requires a Collider to evaluate containment.", this);
                    _loggedMissingCollider = true;
                }

                return false;
            }

            return zoneCollider.bounds.Contains(worldPosition);
        }

        private Collider ResolveCollider()
        {
            if (_collider == null)
                _collider = GetComponent<Collider>();

            return _collider;
        }
    }
}
