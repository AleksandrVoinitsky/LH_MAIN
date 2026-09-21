# Task 4: Baseline Hitscan Weapon And Server Raycast Resolution

## Files

- Create: `Assets/Scripts/Gameplay/Combat/WeaponDefinition.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponRuntimeState.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponFireRequest.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponFireResult.cs`
- Create: `Assets/Scripts/Gameplay/Combat/WeaponRuntime.cs`
- Create: `Assets/Scripts/Gameplay/Combat/BodyZoneHitbox.cs`
- Create: `Assets/Scripts/Gameplay/Combat/ServerRaycastWeaponResolver.cs`
- Test: `Assets/Tests/EditMode/WeaponRuntimeTests.cs`
- Test: `Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs`

## Interfaces

- Consumes: `DamageEvent`, `BodyZone`, `BodyZoneDamageTable`
- Produces: `WeaponDefinition.BaselineRifle() : WeaponDefinition`
- Produces: `WeaponRuntime.TryFire(WeaponFireRequest request, double serverTimeSeconds) : WeaponFireResult`
- Produces: `WeaponRuntime.TryReload(Guid requestId, double serverTimeSeconds) : WeaponFireResult`
- Produces: `BodyZoneHitbox.PlayerId : string`
- Produces: `BodyZoneHitbox.Zone : BodyZone`
- Produces: `ServerRaycastWeaponResolver.TryResolve(Vector3 origin, Vector3 direction, float range, LayerMask mask, out Guid targetPlayerId, out BodyZone bodyZone) : bool`

## Steps

1. Create `Assets/Tests/EditMode/WeaponRuntimeTests.cs` with tests from the plan covering fire ammo/cooldown, reload moving reserve into magazine, and duplicate fire request.
2. Create `Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs` with a temporary cube collider and `BodyZoneHitbox` component. Fire a ray through the collider and assert returned player id/body zone.
3. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-4-editmode.xml`. Expected: FAIL because weapon and raycast types do not exist.
4. Implement baseline rifle defaults: magazine `30`, reserve `90`, fire cooldown `0.1`, reload seconds `2.0`, max range `100`, base damage `30`.
5. `TryFire` rejects `fire_duplicate`, `weapon_cooldown`, `weapon_reloading`, and `ammo_empty`.
6. `TryReload` rejects `reload_duplicate`, `reload_not_needed`, and `reserve_empty`.
7. Implement `BodyZoneHitbox : MonoBehaviour` with serialized player id string and body zone. Add `ConfigureForTest(Guid, BodyZone)` for tests.
8. `ServerRaycastWeaponResolver.TryResolve` normalizes direction, rejects zero direction, uses `Physics.Raycast`, and returns the first hitbox found on the hit collider or parent.
9. Verify with Unity EditMode tests. Expected: weapon runtime and raycast resolver tests pass.
10. Run `git diff --check`. Expected: clean.
11. Commit with `git add Assets/Scripts/Gameplay/Combat Assets/Tests/EditMode/WeaponRuntimeTests.cs Assets/Tests/EditMode/ServerRaycastWeaponResolverTests.cs` and `git commit -m "feat: add baseline hitscan weapon rules"`.

## Global Constraints

- Weapon scope is one baseline server-authoritative hitscan weapon with ammo and reload.
- Client fire input is intent only; server resolves hit, target, body zone, damage, ammo, cooldown, and range.
- Weapon fire respects ammo, reload state, cooldown, and range.
- Damage zones are head, torso, arms, and legs; armor and helmets are out of scope.
- Use standard Unity Physics raycasts, colliders, layers, and masks.
