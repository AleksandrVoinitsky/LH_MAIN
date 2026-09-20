# Task 9: Phase 06 Load Runner And Final Verification

## Files

- Modify: `Assets/Scripts/Load/HeadlessMatchBot.cs`
- Modify: `Assets/Scripts/Load/LoadScenarioReport.cs`
- Modify: `Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs`
- Test: `Assets/Tests/EditMode/HeadlessMatchBotTests.cs`
- Test: `Assets/Tests/EditMode/LoadScenarioReportTests.cs`
- Test: `Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs`
- Create: `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-report.md`

## Interfaces

- Consumes: `NetworkPlayerController.ServerPickupLoot`, `ServerFireWeapon`, `ServerReloadWeapon`, `ServerThrowGrenade`, `ServerUseMed`
- Modifies: `BotScenario.Phase06CoreMatch : bool`
- Modifies: `BotResult.PickedUpLoot`, `FiredWeapon`, `ReloadedWeapon`, `ThrewGrenade`, `UsedMedItem`, `TookZoneDamage`
- Modifies: `LoadScenarioReport` Phase 06 counters and JSON output
- Modifies: `NetworkedCoreLoadRunner` command-line flag `--phase06CoreMatch true`

## Steps

1. Extend `HeadlessMatchBotTests.cs` to assert deterministic Phase 06 action windows. Use helper methods instead of waiting real time.
2. Add this test intent: `HeadlessMatchBot.GetPhase06ActionForElapsedSeconds(1d)` returns `BotPhase06Action.PickupLoot`, `3d` returns `FireWeapon`, `5d` returns `ReloadWeapon`, `7d` returns `ThrowGrenade`, and `9d` returns `UseMedItem`.
3. Extend `LoadScenarioReportTests.cs` so `ToJson` includes Phase 06 counters. Required sample fields/values: `LootPickups = 64`, `DuplicateLootPrevented = 12`, `FireRequests = 64`, `GrenadesExploded = 64`, `ZoneDamageTicks = 10`, `MedItemsUsed = 32`. JSON must contain `"lootPickups":64`, `"duplicateLootPrevented":12`, and `"grenadesExploded":64`.
4. Extend `NetworkedCoreLoadRunnerTests.cs` to assert Phase 06 success requires all clients completed, at least one duplicate loot prevention event, at least one accepted fire request, at least one grenade explosion, at least one zone damage tick, and no duplicate loot success.
5. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode.xml`. Expected: FAIL because Phase 06 bot/report/load-runner fields do not exist.
6. Add `BotPhase06Action` enum with values `Move`, `PickupLoot`, `FireWeapon`, `ReloadWeapon`, `ThrowGrenade`, `UseMedItem`.
7. In `HeadlessMatchBot.RunAsync`, when `Phase06CoreMatch` is true, keep movement running and invoke the matching `NetworkPlayerController` server intent methods in deterministic windows.
8. Set `BotResult` flags only after local invocation succeeds or an accepted replicated state is observed.
9. Add Phase 06 counters to `LoadScenarioReport.ToJson`.
10. Add command-line option parsing in `LoadRunnerOptions`: `--phase06CoreMatch true`, default report path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`.
11. Update `ShouldExitSuccessfully` so Phase 06 requires all clients completed, no duplicate-loot success, and non-zero coverage for pickup, fire, grenade, med, and zone counters.
12. Run Unity EditMode tests with results path `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-editmode.xml`. Expected: all EditMode tests pass.
13. Run `dotnet test server\LH.Main.Server.sln --configuration Release`. Expected: contracts and backend API tests pass.
14. Restart the local backend and game servers from the Phase 06 worktree: `docker compose --env-file .env.example --profile game-servers down --volumes`, then `docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2`.
15. Run the Unity load runner with Phase 06 enabled: `& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -phase06CoreMatch true -clients 64 -durationSeconds 60 -reportPath .superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-load-64.json`.
16. Expected: Unity exits `0`, report has `targetClients:64`, `completedClients:64`, non-zero pickup/fire/grenade/med/zone counters, and duplicate loot prevention evidence without duplicate loot success.
17. Create `.superpowers/sdd/2026-09-21-phase-06-core-match/task-9-final-report.md` with exact command lines, result XML/report paths, pass counts, and any accepted caveats. Include the backend test result and Unity EditMode result path.
18. Commit with `git add Assets/Scripts/Load/HeadlessMatchBot.cs Assets/Scripts/Load/LoadScenarioReport.cs Assets/Scripts/Editor/NetworkedCoreLoadRunner.cs Assets/Tests/EditMode/HeadlessMatchBotTests.cs Assets/Tests/EditMode/LoadScenarioReportTests.cs Assets/Tests/EditMode/NetworkedCoreLoadRunnerTests.cs .superpowers/sdd/2026-09-21-phase-06-core-match` and `git commit -m "feat: add phase 06 load scenario"`.

## Global Constraints

- Runtime-only persistence: match inventory, loot, ammo, effects, med-item use, and extracted loot do not persist after the match.
- Existing Phase 05 match result flow remains the only post-match backend integration.
- Clients send intent only; server owns inventory mutations, damage, projectile hits, grenade explosions, healing, and zone state.
- Phase 05 extraction remains valid without loot requirement, quest requirement, or zone/hazard blocker.
- Failed attempts should increment gameplay rejection metrics with reason codes where the existing metrics shape supports it.
- Do not add backend item tables, stash APIs, item economy, armor, attachments, final content balance, AI, client prediction, custom transport, or production art.
