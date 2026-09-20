using System;
using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public static class ServerRaycastWeaponResolver
    {
        public static bool TryResolve(Vector3 origin, Vector3 direction, float range, LayerMask mask, out Guid targetPlayerId, out BodyZone bodyZone)
        {
            targetPlayerId = Guid.Empty;
            bodyZone = default;

            if (direction.sqrMagnitude <= 0f)
                return false;

            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, range, mask))
                return false;

            BodyZoneHitbox hitbox = hit.collider.GetComponentInParent<BodyZoneHitbox>();
            if (hitbox == null || !Guid.TryParse(hitbox.PlayerId, out targetPlayerId))
            {
                targetPlayerId = Guid.Empty;
                return false;
            }

            bodyZone = hitbox.Zone;
            return true;
        }

        public static bool TryResolveDamage(
            Vector3 origin,
            Vector3 direction,
            float range,
            LayerMask mask,
            WeaponDefinition weaponDefinition,
            BodyZoneDamageTable damageTable,
            Guid? sourcePlayerId,
            Guid correlationId,
            out DamageEvent damageEvent,
            out int calculatedDamage)
        {
            damageEvent = default;
            calculatedDamage = 0;

            if (weaponDefinition == null)
                throw new ArgumentNullException(nameof(weaponDefinition));

            if (damageTable == null)
                throw new ArgumentNullException(nameof(damageTable));

            if (!TryResolve(origin, direction, range, mask, out Guid targetPlayerId, out BodyZone bodyZone))
                return false;

            damageEvent = new DamageEvent(correlationId, sourcePlayerId, targetPlayerId, weaponDefinition.BaseDamage, bodyZone, "hitscan");
            calculatedDamage = damageTable.CalculateDamage(weaponDefinition.BaseDamage, bodyZone);
            return true;
        }
    }
}
