# Phase 06 Core Match Design

## Purpose

Phase 06 turns the current networked technical match into a server-authoritative core match runtime. The server owns inventory, loot, weapons, projectiles, grenades, effects, body-zone damage, healing, and shrinking zone damage. Clients send intent only; clients never author inventory mutations, damage, projectile hits, grenade explosions, or zone state.

This phase remains runtime-only. Match inventory, loot, ammo, effects, and med-item use do not persist after the match. The existing Phase 05 match result flow remains the only post-match backend integration, and extracted loot persistence is deferred to a later meta-game/stash phase.

## Scope

Included:

- Slot-based player inventory with fixed capacity and server-side transactions.
- Server-authored loot spawn points and loot containers placed in the Unity scene.
- Item definitions for weapons, ammo, grenades, med items, and quest/value items.
- One baseline server-authoritative hitscan weapon with ammo and reload.
- Server-simulated projectile/grenade entities with Unity Physics collision checks.
- Body-zone damage for head, torso, arms, and legs.
- Instant med-item use with server-side validation and cooldown.
- Effects runtime for damage-over-time, healing, and zone/hazard damage.
- Scene-authored shrinking zone volumes controlled by the server.
- 64-client load-runner scenario covering loot contention, weapons, grenades, and zone damage.

Not included:

- Persistent player stash, extracted loot ledger, item economy, or backend inventory tables.
- Armor, helmets, attachments, weapon modding, durability, or final balance.
- Client prediction for inventory, shooting, projectile movement, or zone state.
- New extraction requirements. Phase 05 extraction remains valid without loot or quest gating.
- Production art, final weapon models, final map content, AI, or transport replacement.

## Architecture

The implementation should extend the existing Unity/FishNet server runtime rather than introduce a separate gameplay server. `NetworkPlayerController` remains the client intent entry point and should delegate authoritative gameplay operations into server-only runtime services. Pure rule code should live in small C# gameplay classes that can be covered by EditMode tests without requiring a FishNet scene.

Recommended Unity namespaces and folders:

- `Assets/Scripts/Gameplay/Items`: item definitions, item stacks, inventory slots, inventory transactions.
- `Assets/Scripts/Gameplay/Loot`: loot spawn points, loot containers, generated loot instances.
- `Assets/Scripts/Gameplay/Combat`: weapon, ammo, reload, body-zone hit resolution, projectile and grenade rules.
- `Assets/Scripts/Gameplay/Effects`: timed effects, cooldowns, healing, damage-over-time.
- `Assets/Scripts/Gameplay/Zone`: zone phase config, scene volume adapters, zone damage evaluation.
- `Assets/Scripts/Networking`: FishNet RPC/broadcast adapters that expose intent and replicated results.
- `Assets/Scripts/Load`: Phase 06 bot behavior and report metrics.

The server runtime should keep authoritative state in memory for the duration of a match. Replicated FishNet objects and RPCs are adapters around that state, not sources of truth.

## Inventory And Loot

Inventory is slot-based only. A player has a fixed number of slots. Each slot is empty or contains one item stack. Stack limits come from item definitions. There is no weight, grid layout, volume, nested container, or persistent stash in this phase.

Loot is created by server-authored scene components:

- `LootSpawnPoint` creates one or more runtime loot entries at match start or when explicitly reset by the server.
- `LootContainer` owns a server-side collection of loot entries and exposes pickup intent validation.
- Each spawned loot entry has a unique runtime id. The id is consumed atomically during pickup so concurrent clients cannot duplicate loot.

Clients may request pickup/drop/use by sending intent with player id context derived from their FishNet connection. The server validates life state, interaction range, item existence, slot capacity, stack limits, and duplicate transaction ids. Line-of-sight checks are deferred unless the existing scene setup already provides the required colliders and masks without extra map work. Failed attempts should increment gameplay rejection metrics with reason codes.

## Items

Phase 06 item categories are:

- Weapon: the baseline hitscan weapon.
- Ammo: consumed by weapon fire and reload.
- Grenade: consumed when thrown and represented as a server-simulated entity.
- Med item: consumed on successful use and applies an instant heal/effect after cooldown validation.
- Quest/value item: lootable runtime item with no Phase 06 persistence or extraction requirement.

Item definitions should be data-driven enough for tests and technical content, but this phase should avoid building a full content authoring pipeline. ScriptableObjects are acceptable for Unity scene content; pure serializable configs are preferable for rule tests.

## Weapons And Damage

Phase 06 includes one baseline server-authoritative hitscan weapon. The weapon supports ammo in magazine, reserve ammo, reload, fire cooldown, max range, damage, and body-zone multipliers.

Client fire input contains intent only: fire request id, aim origin/direction, and optionally client timestamp for diagnostics. The server resolves the shot using Unity Physics raycasts, collision layers, and masks. The server decides whether a hit occurred, which player was hit, which body zone was hit, how much damage applies, and whether ammo is consumed.

Damage uses body zones:

- Head: highest multiplier.
- Torso: baseline multiplier.
- Arms: reduced multiplier.
- Legs: reduced multiplier. Movement-impairing leg effects are deferred.

The existing `PlayerStateMachine` should be extended or wrapped to support typed damage and healing while preserving terminal-state safety. Damage and healing are idempotent by correlation id where repeated network delivery is possible.

## Projectiles And Grenades

Grenades and any non-hitscan projectiles are server-simulated spawned entities. They move on server ticks, use Unity Physics collisions, and produce replicated events for clients. Clients do not simulate authoritative grenade movement or explosion results.

The initial grenade supports:

- Throw intent validation from inventory.
- Server-side spawn position/direction validation.
- Fuse-triggered explosion using a server-owned timer.
- Radius damage with distance falloff.
- Single authoritative explosion event with duplicate prevention.

Projectile/grenade logic should be testable without production art. A simple prefab or runtime-created object is sufficient if it provides colliders, physics settings, and FishNet replication hooks needed by load tests.

## Effects And Med Items

Effects are runtime-only and server-owned. They cover immediate healing, damage-over-time, cooldowns, and zone/hazard damage markers. Effects do not persist after match completion.

Med items apply instantly. The client sends a use intent. The server validates that the player is alive or wounded as allowed by config, owns the med item, is not on cooldown, and has health missing. On success, the server consumes the item, applies healing, records the cooldown, and replicates the inventory and health result.

Use duration and channel interruption are explicitly deferred.

## Shrinking Zone

The shrinking zone is authored in the Unity scene by server-owned zone volume components. Designers or tests define phase timings, damage rates, and target volume states through scene/config data. During the match, the server advances zone phases and evaluates player positions against the current safe/hazard volumes.

The server applies zone damage/effects on ticks. Clients receive replicated zone state and events for UI/diagnostics only. Zone state cannot be changed by clients.

The implementation should start with one safe/hazard volume model and leave room for later anomaly volumes, but Phase 06 does not require multiple independent anomaly systems.

## Extraction And Match Result

Phase 05 extraction remains unchanged. Extraction has no loot requirement, quest requirement, or zone/hazard blocker in Phase 06. Match finalization continues to submit the existing match result payload and reward code behavior.

Runtime loot and inventory state may be included in local load-runner diagnostics, but it must not create backend persistence or change the backend reward ledger in this phase.

## Networking And Replication

Client-to-server operations are intent RPCs:

- Move input, as already implemented.
- Pickup/drop/use item intent.
- Equip weapon intent.
- Fire/reload intent.
- Throw grenade intent.

Server-to-client communication publishes accepted results and state snapshots:

- Inventory slot changes.
- Loot entry removed/created.
- Health/life-state changes.
- Weapon ammo/reload state.
- Projectile/grenade spawn, movement samples if needed, and explosion events.
- Zone phase/state changes.

FishNet observers and ownership should be configured so players receive relevant state without granting authority over shared loot or combat results. If bandwidth becomes a concern, optimize replication after correctness tests pass.

## Error Handling And Security

Every gameplay intent should fail closed. Invalid requests should return no mutation and record a reason-coded rejection metric. Required validation includes:

- Player connection owns the requested player entity.
- Player life state allows the action.
- Target loot/item/weapon exists in authoritative state.
- Requested inventory operation fits capacity and stack limits.
- Weapon fire respects ammo, reload state, cooldown, and range.
- Projectile/grenade spawn parameters are clamped to server-authorized values.
- Damage/healing correlation ids prevent duplicate application where needed.

The server should not trust client-reported hits, damage amounts, inventory content, zone membership, or extracted loot.

## Metrics And Load Readiness

Phase 06 should extend technical metrics and the load report with counters for:

- Accepted/rejected pickup attempts.
- Duplicate loot pickup prevention.
- Inventory full rejections.
- Accepted/rejected fire requests.
- Hitscan hit/miss counts.
- Grenades thrown/exploded.
- Zone damage ticks.
- Med items used/rejected.
- Final alive/dead/extracted/disconnected counts.

The required load-readiness target is one 64-client technical match. The scenario must exercise loot pickup contention, inventory capacity, baseline weapon fire, reload/ammo consumption, grenade explosion, med item use, and shrinking zone damage. The report should fail if duplicate loot is detected or if any client-authoritative shortcut is needed to complete the scenario.

## Testing Strategy

Use tests before implementation for each subsystem boundary.

EditMode unit tests:

- Inventory capacity, stacking, pickup, drop, use, and duplicate transaction rejection.
- Loot container atomic pickup under repeated or concurrent requests.
- Weapon ammo, reload, cooldown, range, body-zone damage, and duplicate fire request handling.
- Player health, damage, healing, terminal-state, and idempotency transitions.
- Grenade fuse/explosion single-application and radius falloff rules.
- Zone phase transitions and damage tick rules.

Unity integration/EditMode tests:

- Physics raycast hit resolution against configured layers and body-zone colliders.
- Scene-authored loot spawn point/container initialization.
- Zone volume membership evaluation.
- FishNet-facing intent handlers reject unauthorized or malformed requests.

Backend tests:

- Existing Phase 05 match result tests remain passing.
- No new backend persistence tables or APIs are required for Phase 06.

Load verification:

- Extend the current `NetworkedCoreLoadRunner` and `HeadlessMatchBot` path with a Phase 06 scenario flag/report path.
- Run 64 clients through movement, loot, weapon, grenade, med, zone, and finalization checks.
- Preserve Phase 05 load coverage unless explicitly replaced by an equivalent superset scenario.

## Rollout Plan Shape

The implementation plan should be split into independently reviewable tasks:

1. Runtime item definitions and inventory rules with tests.
2. Server-authored loot spawn/container runtime with duplicate-prevention tests.
3. Player health/body-zone damage/healing model extensions.
4. Baseline hitscan weapon, ammo, reload, and server raycast validation.
5. Grenade/projectile server simulation and explosion damage.
6. Effects and instant med-item use.
7. Scene-authored shrinking zone phases and damage.
8. FishNet RPC/replication adapters and metrics.
9. Phase 06 64-client load-runner scenario and final verification report.

Each task should keep authoritative rule code testable outside full play mode where practical, then add Unity/FishNet tests only where scene physics or networking behavior is the point.
