# Task 5: Grenade Runtime And Explosion Damage

## Files

- Create: `Assets/Scripts/Gameplay/Combat/GrenadeDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeThrowRequest.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeExplosionResult.cs`
- Create: `Assets/Scripts/Gameplay/Combat/GrenadeRuntime.cs`
- Test: `Assets/Tests/EditMode/GrenadeRuntimeTests.cs`

## Interfaces

- Consumes: `DamageEvent`, `BodyZone.Torso`
- Produces: `GrenadeDefinition.StandardFrag() : GrenadeDefinition`
- Produces: `GrenadeRuntime.Tick(double serverTimeSeconds, IReadOnlyList<GrenadeTarget> targets) : GrenadeExplosionResult`
- Produces: `GrenadeRuntime.HasExploded : bool`
- Produces: `GrenadeExplosionResult.DamageEvents : IReadOnlyList<DamageEvent>`

## Steps

1. Create `Assets/Tests/EditMode/GrenadeRuntimeTests.cs` with tests from the plan covering single fuse explosion and distance falloff.
2. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-5-editmode.xml`. Expected: FAIL because grenade runtime types do not exist.
3. Implement grenade runtime.
4. Create `GrenadeTarget(Guid playerId, Vector3 position)` in the same file or a focused file.
5. `GrenadeRuntime` stores grenade id, spawn position, spawn time, definition, and exploded flag.
6. `Tick` returns no mutation before fuse time, then creates torso `DamageEvent`s for targets within radius.
7. Damage formula: `ceil(maxDamage * (1 - distance / radius))`, clamped to at least `1` for targets inside radius.
8. After explosion, later ticks return `Exploded = false` with an empty damage list.
9. Verify with Unity EditMode tests. Expected: grenade tests pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Combat Assets/Tests/EditMode/GrenadeRuntimeTests.cs` and `git commit -m "feat: add server grenade runtime"`.

## Global Constraints

- Grenades are server-simulated entities with Unity Physics collision checks and fuse-triggered explosion.
- The initial grenade supports server-side spawn position/direction validation, fuse-triggered explosion, radius damage with distance falloff, and single authoritative explosion event with duplicate prevention.
- Clients do not simulate authoritative grenade movement or explosion results.
- Damage uses body zones; grenade damage is torso damage for Phase 06.
