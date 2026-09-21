# Task 7: Scene-Authored Shrinking Zone

## Files

- Create: `Assets/Scripts/Gameplay/Zone/ZonePhase.cs`
- Create: `Assets/Scripts/Gameplay/Zone/ZoneRuntime.cs`
- Create: `Assets/Scripts/Gameplay/Zone/ZoneVolume.cs`
- Test: `Assets/Tests/EditMode/ZoneRuntimeTests.cs`
- Test: `Assets/Tests/EditMode/ZoneVolumeTests.cs`

## Interfaces

- Produces: `ZonePhase(double startsAtSeconds, double endsAtSeconds, int damagePerTick, double tickIntervalSeconds)`
- Produces: `ZoneRuntime(IReadOnlyList<ZonePhase> phases)`
- Produces: `ZoneRuntime.GetActivePhase(double elapsedSeconds) : ZonePhase`
- Produces: `ZoneRuntime.ShouldApplyDamage(Guid playerId, bool isInsideSafeVolume, double elapsedSeconds) : bool`
- Produces: `ZoneVolume.Contains(Vector3 worldPosition) : bool`

## Steps

1. Create `Assets/Tests/EditMode/ZoneRuntimeTests.cs` with tests from the plan covering active phase selection and damage tick cadence outside safe volume.
2. Create `Assets/Tests/EditMode/ZoneVolumeTests.cs` with a `BoxCollider` trigger and `ZoneVolume.Contains` assertions for inside/outside points.
3. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-7-editmode.xml`. Expected: FAIL because zone runtime types do not exist.
4. Implement zone runtime and volume.
5. `ZoneRuntime` sorts phases by start time, rejects an empty phase list in the constructor, and stores last damage tick per player.
6. `ShouldApplyDamage` returns false inside safe volume, outside active phases, or before the player's next tick time.
7. `ZoneVolume : MonoBehaviour` requires a collider reference or discovers one on the same object.
8. `Contains` uses collider bounds for the first implementation. If the collider is missing, return false and log a warning once.
9. Verify with Unity EditMode tests. Expected: zone tests pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Zone Assets/Tests/EditMode/ZoneRuntimeTests.cs Assets/Tests/EditMode/ZoneVolumeTests.cs` and `git commit -m "feat: add shrinking zone runtime"`.

## Global Constraints

- Shrinking zone uses server-owned Unity scene volumes; clients receive replicated state/events only.
- The server advances zone phases and evaluates player positions against current safe/hazard volumes.
- The server applies zone damage/effects on ticks.
- Zone state cannot be changed by clients.
- Start with one safe/hazard volume model; multiple anomaly systems are out of scope.
