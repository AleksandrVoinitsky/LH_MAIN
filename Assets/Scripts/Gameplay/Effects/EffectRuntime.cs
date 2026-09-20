using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay.Items;

namespace LH.Main.Unity.Gameplay.Effects
{
    public sealed class EffectRuntime
    {
        private const double MedCooldownSeconds = 1d;
        private readonly Dictionary<Guid, double> _nextMedUseAtSeconds = new Dictionary<Guid, double>();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();

        public InventoryTransactionResult TryUseMedItem(
            Guid playerId,
            PlayerInventory inventory,
            PlayerStateMachine state,
            string itemId,
            int healAmount,
            double serverTimeSeconds,
            Guid transactionId)
        {
            return InventoryTransactionResult.Rejected("med_item_definition_required");
        }

        public InventoryTransactionResult TryUseMedItem(
            Guid playerId,
            PlayerInventory inventory,
            PlayerStateMachine state,
            ItemDefinition itemDefinition,
            int healAmount,
            double serverTimeSeconds,
            Guid transactionId)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (itemDefinition.Category != ItemCategory.MedItem || !itemDefinition.Usable)
                return InventoryTransactionResult.Rejected("med_item_invalid");

            if (_nextMedUseAtSeconds.TryGetValue(playerId, out double nextUseAtSeconds) && serverTimeSeconds < nextUseAtSeconds)
                return InventoryTransactionResult.Rejected("med_cooldown");

            InventoryTransactionResult remove = inventory.TryRemove(itemDefinition.ItemId, 1, transactionId);
            if (!remove.Accepted)
                return remove;

            PlayerStateChange healing = state.ApplyHealing(new HealingEvent(transactionId, playerId, healAmount, itemDefinition.ItemId));
            if (!healing.Accepted)
            {
                inventory.TryAdd(itemDefinition, 1, Guid.NewGuid());
                return InventoryTransactionResult.Rejected(healing.Reason);
            }

            _nextMedUseAtSeconds[playerId] = serverTimeSeconds + MedCooldownSeconds;
            return remove;
        }

        public void AddDamageOverTime(Guid playerId, int damagePerTick, double tickIntervalSeconds, double expiresAtSeconds)
        {
            _effects.Add(new TimedEffect(
                Guid.NewGuid(),
                playerId,
                EffectKind.DamageOverTime,
                damagePerTick,
                tickIntervalSeconds,
                double.NegativeInfinity,
                expiresAtSeconds));
        }

        public int Tick(double serverTimeSeconds, Func<Guid, int, string, Guid, bool> applyDamage)
        {
            if (applyDamage == null)
                throw new ArgumentNullException(nameof(applyDamage));

            int applied = 0;
            for (int index = _effects.Count - 1; index >= 0; index--)
            {
                TimedEffect effect = _effects[index];
                if (serverTimeSeconds >= effect.ExpiresAtSeconds)
                {
                    _effects.RemoveAt(index);
                    continue;
                }

                if (serverTimeSeconds < effect.NextTickAtSeconds)
                    continue;

                if (effect.Kind == EffectKind.DamageOverTime && applyDamage(effect.PlayerId, effect.AmountPerTick, "damage_over_time", Guid.NewGuid()))
                    applied++;

                _effects[index] = effect.WithNextTick(serverTimeSeconds + effect.TickIntervalSeconds);
            }

            return applied;
        }
    }
}
