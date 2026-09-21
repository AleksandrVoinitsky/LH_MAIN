# Task 8: Core Match Runtime, FishNet Intent Adapters, And Metrics

## Files

- Create: `Assets/Scripts/Gameplay/CoreMatchRuntime.cs`
- Modify: `Assets/Scripts/Networking/NetworkPlayerController.cs`
- Modify: `Assets/Scripts/Networking/GameServerMetrics.cs`
- Modify: `Assets/Scripts/Server/GameServerStatus.cs`
- Modify: `Assets/Scripts/Server/GameServerBootstrap.cs`
- Test: `Assets/Tests/EditMode/NetworkPlayerControllerTests.cs`
- Test: `Assets/Tests/EditMode/GameServerStatusTests.cs`

## Interfaces

- Consumes: inventory, loot, damage, weapon, grenade, effects, and zone interfaces from Tasks 1-7.
- Produces: `CoreMatchRuntime.RegisterPlayer(Guid playerId) : void`
- Produces: `CoreMatchRuntime.TryPickup(Guid playerId, Guid lootId, Guid transactionId, Vector3 playerPosition) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.TryUseMed(Guid playerId, string itemId, Guid transactionId, double serverTimeSeconds) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.TryFire(Guid playerId, WeaponFireRequest request, Vector3 origin, Vector3 direction, double serverTimeSeconds) : WeaponFireResult`
- Produces: `CoreMatchRuntime.TryThrowGrenade(Guid playerId, Guid transactionId, Vector3 origin, Vector3 direction, double serverTimeSeconds) : InventoryTransactionResult`
- Produces: `CoreMatchRuntime.Tick(double serverTimeSeconds) : void`
- Modifies: `NetworkPlayerController.ServerPickupLoot(Guid lootId, Guid transactionId)`
- Modifies: `NetworkPlayerController.ServerUseMed(string itemId, Guid transactionId)`
- Modifies: `NetworkPlayerController.ServerFireWeapon(Guid requestId, Vector3 origin, Vector3 direction)`
- Modifies: `NetworkPlayerController.ServerReloadWeapon(Guid requestId)`
- Modifies: `NetworkPlayerController.ServerThrowGrenade(Guid transactionId, Vector3 origin, Vector3 direction)`

## Steps

1. Extend `GameServerStatusTests.cs` to assert JSON includes Phase 06 fields: `acceptedPickupAttempts`, `rejectedPickupAttempts`, `duplicateLootPickups`, `acceptedFireRequests`, `rejectedFireRequests`, `grenadesExploded`, `zoneDamageTicks`, and `medItemsUsed`.
2. Extend `NetworkPlayerControllerTests.cs` to assert new intent methods have `[ServerRpc]` and terminal players cannot fire/use/pickup through test-facing server helper methods.
3. Add tests that require exact helper signatures on `NetworkPlayerController`: `ApplyServerPickupForTest(CoreMatchRuntime runtime, Guid lootId, Guid transactionId)`, `ApplyServerUseMedForTest(CoreMatchRuntime runtime, string itemId, Guid transactionId)`, and `ApplyServerFireForTest(CoreMatchRuntime runtime, Guid requestId, Vector3 origin, Vector3 direction)`. Each helper must have `[Server]` and must return the same result type as the underlying runtime call.
4. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-8-editmode.xml`. Expected: FAIL because runtime, adapters, and metrics fields do not exist.
5. Add counters to `GameServerMetrics`: accepted/rejected pickups, duplicate loot pickups, inventory-full rejections, accepted/rejected fire requests, hitscan hits, hitscan misses, grenades thrown, grenades exploded, zone damage ticks, med items used, med items rejected.
6. Extend `Snapshot`, `GameServerBootstrap.CreateStatus`, `GameServerStatus`, and `GameServerStatus.ToJson` with these fields.
7. Implement `CoreMatchRuntime` composing per-player `PlayerInventory`, `PlayerStateMachine`, `WeaponRuntime`, active grenades, loot containers, effects, and zone runtime.
8. Start with technical defaults so tests/load runner can operate without production content: 8 inventory slots, one baseline rifle, 30 ammo magazine, 90 reserve ammo, one med item, one grenade, and one value item spawn.
9. Every public method must check registered player and terminal life state before mutation.
10. Return stable rejection reasons: `player_not_registered`, `state_terminal`, `loot_not_found`, `item_not_owned`, `fire_rejected`, `grenade_rejected`, or subsystem-specific reason.
11. Add `[ServerRpc]` methods to `NetworkPlayerController` that derive the authoritative player id from registry/runtime context rather than trusting a client-provided id.
12. For tests, allow injecting a `CoreMatchRuntime` reference through a server-only configure method.
13. Do not replicate final UI; only expose accepted result state enough for load runner and metrics.
14. In `GameServerBootstrap`, initialize one `CoreMatchRuntime` during server startup, register players through existing registry integration, and call `CoreMatchRuntime.Tick(Time.realtimeSinceStartupAsDouble)` from `OnPostTick` after existing tick metrics logic.
15. Verify with Unity EditMode tests. Expected: network adapter, status, and all previous gameplay tests pass.
16. Run `dotnet test server\LH.Main.Server.sln --configuration Release`. Expected: contracts and backend API tests pass; no backend persistence changes were added.
17. Run `git diff --check`. Expected: clean.
18. Commit with `git add Assets/Scripts/Gameplay/CoreMatchRuntime.cs Assets/Scripts/Networking/NetworkPlayerController.cs Assets/Scripts/Networking/GameServerMetrics.cs Assets/Scripts/Server/GameServerStatus.cs Assets/Scripts/Server/GameServerBootstrap.cs Assets/Tests/EditMode/NetworkPlayerControllerTests.cs Assets/Tests/EditMode/GameServerStatusTests.cs` and `git commit -m "feat: wire core match runtime"`.

## Global Constraints

- Runtime-only persistence: match inventory, loot, ammo, effects, med-item use, and extracted loot do not persist after the match.
- Existing Phase 05 match result flow remains the only post-match backend integration.
- Clients send intent only; server owns inventory mutations, damage, projectile hits, grenade explosions, healing, and zone state.
- Phase 05 extraction remains valid without loot requirement, quest requirement, or zone/hazard blocker.
- Failed attempts should increment gameplay rejection metrics with reason codes where the existing metrics shape supports it.
- Do not add backend item tables, stash APIs, item economy, armor, attachments, final content balance, AI, client prediction, custom transport, or production art.
