using System;
using System.Collections.Generic;
using LH.Main.Unity.Gameplay.Effects;
using LH.Main.Unity.Gameplay.Items;
using UnityEngine;

namespace LH.Main.Unity.Gameplay
{
    public sealed class CoreMatchRuntime
    {
        public static readonly Guid InitialLootId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private const int InventorySlots = 8;
        private const int MedHealAmount = 35;
        private const float LootInteractionRange = 3f;
        private const string MedItemId = "med_basic";
        private const string GrenadeItemId = "grenade_frag";
        private const string ValueItemId = "value_item";

        private readonly Dictionary<Guid, PlayerRuntime> _players = new Dictionary<Guid, PlayerRuntime>();
        private readonly Dictionary<string, ItemDefinition> _itemDefinitions = new Dictionary<string, ItemDefinition>();
        private readonly Dictionary<Guid, LootRuntimeEntry> _loot = new Dictionary<Guid, LootRuntimeEntry>();
        private readonly List<GrenadeRuntime> _activeGrenades = new List<GrenadeRuntime>();
        private readonly BodyZoneDamageTable _damageTable = BodyZoneDamageTable.Standard();
        private readonly GrenadeDefinition _grenadeDefinition = GrenadeDefinition.StandardFrag();
        private readonly EffectRuntime _effects = new EffectRuntime();
        private readonly ZoneRuntime _zone = new ZoneRuntime(new[] { new ZonePhase(0d, double.MaxValue, 5, 1d) });
        private readonly double _startedAtSeconds;
        private ZoneVolume _safeZoneVolume;

        public CoreMatchRuntime()
        {
            _startedAtSeconds = Time.realtimeSinceStartupAsDouble;
            RegisterDefinition(new ItemDefinition(MedItemId, ItemCategory.MedItem, 5, true));
            RegisterDefinition(new ItemDefinition(GrenadeItemId, ItemCategory.Grenade, 5, true));
            RegisterDefinition(new ItemDefinition(ValueItemId, ItemCategory.QuestValue, 99, false));
            _loot.Add(InitialLootId, new LootRuntimeEntry(new ItemStack(ValueItemId, 1), Vector3.zero));
        }

        public int LastTickZoneDamageTicks { get; private set; }
        public int LastTickGrenadesExploded { get; private set; }

        public void ConfigureZoneVolume(ZoneVolume safeZoneVolume)
        {
            _safeZoneVolume = safeZoneVolume;
        }

        public void RegisterPlayer(Guid playerId)
        {
            if (playerId == Guid.Empty || _players.ContainsKey(playerId))
                return;

            var inventory = new PlayerInventory(InventorySlots);
            inventory.TryAdd(_itemDefinitions[MedItemId], 1, Guid.NewGuid());
            inventory.TryAdd(_itemDefinitions[GrenadeItemId], 1, Guid.NewGuid());
            _players.Add(playerId, new PlayerRuntime(inventory, PlayerStateMachine.Create(playerId), new WeaponRuntime(WeaponDefinition.BaselineRifle())));
        }

        public InventoryTransactionResult TryPickup(Guid playerId, Guid lootId, Guid transactionId, Vector3 playerPosition)
        {
            if (!TryGetActivePlayer(playerId, out PlayerRuntime player, out InventoryTransactionResult rejection))
                return rejection;

            player.Position = playerPosition;
            if (!_loot.TryGetValue(lootId, out LootRuntimeEntry entry))
                return InventoryTransactionResult.Rejected("loot_not_found");

            if (Vector3.Distance(playerPosition, entry.Position) > LootInteractionRange)
                return InventoryTransactionResult.Rejected("interaction_out_of_range");

            if (!_itemDefinitions.TryGetValue(entry.Stack.ItemId, out ItemDefinition definition))
                return InventoryTransactionResult.Rejected("loot_item_unknown");

            InventoryTransactionResult result = player.Inventory.TryAdd(definition, entry.Stack.Quantity, transactionId);
            if (result.Accepted)
                _loot.Remove(lootId);

            return result;
        }

        public InventoryTransactionResult TryUseMed(Guid playerId, string itemId, Guid transactionId, double serverTimeSeconds)
        {
            if (!TryGetActivePlayer(playerId, out PlayerRuntime player, out InventoryTransactionResult rejection))
                return rejection;

            InventoryTransactionResult result = _effects.TryUseMedItem(playerId, player.Inventory, player.State, itemId, MedHealAmount, serverTimeSeconds, transactionId);
            return result.Accepted || result.Reason != "item_not_found"
                ? result
                : InventoryTransactionResult.Rejected("item_not_owned");
        }

        public WeaponFireResult TryFire(Guid playerId, WeaponFireRequest request, Vector3 origin, Vector3 direction, double serverTimeSeconds)
        {
            if (!TryGetActivePlayer(playerId, out PlayerRuntime player, out WeaponFireResult rejection))
                return rejection;

            if (!IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude <= 0.0000001f)
                return new WeaponFireResult(false, "fire_rejected", player.Weapon.State);

            player.Position = origin;
            WeaponFireResult result = player.Weapon.TryFire(request, serverTimeSeconds);
            if (!result.Accepted)
                return result;

            bool hit = ServerRaycastWeaponResolver.TryResolveDamage(
                origin,
                direction,
                player.Weapon.Definition.MaxRange,
                Physics.DefaultRaycastLayers,
                player.Weapon.Definition,
                _damageTable,
                playerId,
                request.RequestId,
                out DamageEvent damage,
                out _);
            if (hit)
                ApplyDamage(damage);

            return new WeaponFireResult(true, string.Empty, result.State, hit);
        }

        public InventoryTransactionResult TryThrowGrenade(Guid playerId, Guid transactionId, Vector3 origin, Vector3 direction, double serverTimeSeconds)
        {
            if (!TryGetActivePlayer(playerId, out PlayerRuntime player, out InventoryTransactionResult rejection))
                return rejection;

            if (!IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude <= 0.0000001f)
                return InventoryTransactionResult.Rejected("grenade_rejected");

            InventoryTransactionResult remove = player.Inventory.TryRemove(GrenadeItemId, 1, transactionId);
            if (!remove.Accepted)
                return remove.Reason == "item_not_found" ? InventoryTransactionResult.Rejected("item_not_owned") : remove;

            player.Position = origin;
            _activeGrenades.Add(new GrenadeRuntime(new GrenadeThrowRequest(transactionId, playerId, origin, direction), _grenadeDefinition, serverTimeSeconds));
            return remove;
        }

        public WeaponFireResult TryReload(Guid playerId, Guid requestId, double serverTimeSeconds)
        {
            if (!TryGetActivePlayer(playerId, out PlayerRuntime player, out WeaponFireResult rejection))
                return rejection;

            return player.Weapon.TryReload(requestId, serverTimeSeconds);
        }

        public void Tick(double serverTimeSeconds)
        {
            LastTickGrenadesExploded = 0;
            LastTickZoneDamageTicks = 0;

            var targets = new List<GrenadeTarget>(_players.Count);
            foreach (KeyValuePair<Guid, PlayerRuntime> player in _players)
            {
                if (!player.Value.State.LifeState.IsTerminal())
                    targets.Add(new GrenadeTarget(player.Key, player.Value.Position));
            }

            for (int index = _activeGrenades.Count - 1; index >= 0; index--)
            {
                GrenadeExplosionResult explosion = _activeGrenades[index].Tick(serverTimeSeconds, targets);
                if (!explosion.Exploded)
                    continue;

                LastTickGrenadesExploded++;
                for (int eventIndex = 0; eventIndex < explosion.DamageEvents.Count; eventIndex++)
                    ApplyDamage(explosion.DamageEvents[eventIndex]);

                _activeGrenades.RemoveAt(index);
            }

            LastTickZoneDamageTicks += _effects.Tick(serverTimeSeconds, ApplyTechnicalDamage);
            double elapsedSeconds = Math.Max(0d, serverTimeSeconds - _startedAtSeconds);
            ZonePhase activePhase = _zone.GetActivePhase(elapsedSeconds);
            if (activePhase == null)
                return;

            foreach (KeyValuePair<Guid, PlayerRuntime> player in _players)
            {
                if (player.Value.State.LifeState.IsTerminal())
                    continue;

                bool isInsideSafeVolume = _safeZoneVolume == null || _safeZoneVolume.Contains(player.Value.Position);
                if (_zone.ShouldApplyDamage(player.Key, isInsideSafeVolume, elapsedSeconds))
                {
                    if (ApplyTechnicalDamage(player.Key, activePhase.DamagePerTick, "zone", Guid.NewGuid()))
                        LastTickZoneDamageTicks++;
                }
            }
        }

        private void RegisterDefinition(ItemDefinition definition)
        {
            _itemDefinitions[definition.ItemId] = definition;
            _effects.RegisterItemDefinition(definition);
        }

        private bool TryGetActivePlayer(Guid playerId, out PlayerRuntime player, out InventoryTransactionResult rejection)
        {
            if (!_players.TryGetValue(playerId, out player))
            {
                rejection = InventoryTransactionResult.Rejected("player_not_registered");
                return false;
            }

            if (player.State.LifeState.IsTerminal())
            {
                rejection = InventoryTransactionResult.Rejected("state_terminal");
                return false;
            }

            rejection = default;
            return true;
        }

        private bool TryGetActivePlayer(Guid playerId, out PlayerRuntime player, out WeaponFireResult rejection)
        {
            if (!_players.TryGetValue(playerId, out player))
            {
                rejection = new WeaponFireResult(false, "player_not_registered", default);
                return false;
            }

            if (player.State.LifeState.IsTerminal())
            {
                rejection = new WeaponFireResult(false, "state_terminal", player.Weapon.State);
                return false;
            }

            rejection = default;
            return true;
        }

        private bool ApplyTechnicalDamage(Guid playerId, int amount, string kind, Guid correlationId)
        {
            if (!_players.TryGetValue(playerId, out PlayerRuntime player) || player.State.LifeState.IsTerminal())
                return false;

            return player.State.ApplyDamage(new TechnicalDamageEvent(correlationId, null, playerId, amount, kind)).Accepted;
        }

        private void ApplyDamage(DamageEvent damage)
        {
            if (_players.TryGetValue(damage.TargetPlayerId, out PlayerRuntime player) && !player.State.LifeState.IsTerminal())
                player.State.ApplyDamage(damage, _damageTable);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private sealed class PlayerRuntime
        {
            public PlayerRuntime(PlayerInventory inventory, PlayerStateMachine state, WeaponRuntime weapon)
            {
                Inventory = inventory;
                State = state;
                Weapon = weapon;
            }

            public PlayerInventory Inventory { get; }
            public PlayerStateMachine State { get; }
            public WeaponRuntime Weapon { get; }
            public Vector3 Position { get; set; }
        }

        private readonly struct LootRuntimeEntry
        {
            public LootRuntimeEntry(ItemStack stack, Vector3 position)
            {
                Stack = stack;
                Position = position;
            }

            public ItemStack Stack { get; }
            public Vector3 Position { get; }
        }
    }
}
