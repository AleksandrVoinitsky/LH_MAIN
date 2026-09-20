# Task 1 Report: Backend Result Contract And Persistence

## What I implemented

- Added shared result submission DTO records to `MatchmakingContracts.cs`:
  - `MatchResultSubmissionRequest`
  - `MatchResultParticipantRequest`
  - `MatchResultSubmissionResponse`
- Added `MatchSessionStatuses.Completed = "completed"`.
- Added persistence entities:
  - `MatchResult`
  - `MatchResultParticipant`
  - `RewardTransaction`
- Added `AppDbContext` DbSets and EF mappings for:
  - `match_results`
  - `match_result_participants`
  - `reward_transactions`
- Generated EF migration `20260920134254_AddMatchResults` and updated the model snapshot.
- Added focused migration test `MatchResultServiceTests.DatabaseContainsMatchResultRewardTablesAfterMigration`.
- Did not implement service logic or endpoints.
- Did not add wallet/profile balance fields; rewards are represented only by `reward_transactions` persistence.

## What I tested and test results

- RED: `dotnet test server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~MatchResultServiceTests`
  - Result: failed as expected because `match_results` was absent after migration.
- GREEN: `dotnet test server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~MatchResultServiceTests`
  - Result: passed, 1 test passed.
- Whitespace check: `git diff --check -- server/src server/tests`
  - Result: no whitespace errors. Git emitted line-ending warnings for touched files.

## TDD Evidence: RED command/output

Command:

```powershell
dotnet test server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

Output excerpt:

```text
[xUnit.net 00:00:09.89]     LH.Main.Backend.Api.Tests.MatchResultServiceTests.DatabaseContainsMatchResultRewardTablesAfterMigration [FAIL]
  Не пройден LH.Main.Backend.Api.Tests.MatchResultServiceTests.DatabaseContainsMatchResultRewardTablesAfterMigration [2 s]
  Сообщение об ошибке:
   Assert.Contains() Failure: Item not found in collection
Collection: ["__EFMigrationsHistory", "users", "password_credentials", "player_profiles", "game_server_slots", ...]
Not found:  "match_results"

Не пройден!: не пройдено     1, пройдено     0, пропущено     0, всего     1, длительность 2 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

## TDD Evidence: GREEN command/output

Command:

```powershell
dotnet test server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

Output:

```text
Определение проектов для восстановления...
Все проекты обновлены для восстановления.
LH.Main.Contracts -> D:\LH_MAIN-phase-02\server\src\LH.Main.Contracts\bin\Release\net10.0\LH.Main.Contracts.dll
LH.Main.Backend.Api -> D:\LH_MAIN-phase-02\server\src\LH.Main.Backend.Api\bin\Release\net10.0\LH.Main.Backend.Api.dll
LH.Main.Backend.Api.Tests -> D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll
Тестовый запуск для D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll (.NETCoreApp,Version=v10.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено     1, пропущено     0, всего     1, длительность 1 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

Whitespace command:

```powershell
git diff --check -- server/src server/tests
```

Output:

```text
warning: in the working copy of 'server/src/LH.Main.Backend.Api/Persistence/AppDbContext.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'server/src/LH.Main.Backend.Api/Persistence/Entities/MatchSession.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'server/src/LH.Main.Contracts/MatchmakingContracts.cs', LF will be replaced by CRLF the next time Git touches it
```

## Files changed

- `server/src/LH.Main.Contracts/MatchmakingContracts.cs`
- `server/src/LH.Main.Backend.Api/Persistence/AppDbContext.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchSession.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchResult.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Entities/MatchResultParticipant.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Entities/RewardTransaction.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Migrations/20260920134254_AddMatchResults.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Migrations/20260920134254_AddMatchResults.Designer.cs`
- `server/src/LH.Main.Backend.Api/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `server/tests/LH.Main.Backend.Api.Tests/MatchResultServiceTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-1-report.md`

## Self-review findings

- Migration creates `match_results`, `match_result_participants`, and `reward_transactions`.
- `match_results.match_id` is unique and restricts delete to `matches`.
- `match_result_participants.match_result_id` cascades delete from `match_results`.
- `reward_transactions` has unique `(player_id, match_result_id, reward_code)` and restricts delete from `match_results`.
- No gameplay service logic, API endpoints, wallet balance fields, or profile balance fields were added.
- The focused migration test uses the real EF migration path against the PostgreSQL fixture.

## Issues or concerns

- No functional concerns.
- `git diff --check` emitted CRLF/LF conversion warnings for existing line-ending behavior, but no whitespace errors.
