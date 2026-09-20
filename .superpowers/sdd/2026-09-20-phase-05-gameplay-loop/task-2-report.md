# Task 2 Report: Backend Result Service Idempotency

## What I Implemented

- Added `MatchResultService.SubmitAsync(MatchResultSubmissionRequest request, string serverKey, CancellationToken cancellationToken)`.
- Added `MatchResultSubmissionResult` with `Unauthorized`, `ConflictCode`, and `Response` fields plus factory helpers.
- Implemented shared game-server key rejection.
- Implemented request validation conflict codes for invalid result data, missing participants, invalid participant IDs, unsupported outcomes, and unsupported reward codes.
- Implemented match lookup and server mismatch conflict handling.
- Implemented payload hashing for result idempotency.
- Persisted `MatchResult`, `MatchResultParticipant`, and `RewardTransaction` rows inside a transaction for first-time submissions.
- Returned duplicate accepted responses without adding reward transactions when the same `ResultId` and payload are submitted again.
- Returned `result_conflict` when the same `ResultId` is submitted with a different payload.
- Marked the match status as `completed` after accepting a new result.
- Applied reward amount rules: `extracted = 10`, `dead/disconnected = 1`.

## What I Tested And Test Results

- Added `SubmitAsyncRejectsInvalidServerKey`.
- Added `SubmitAsyncPersistsResultAndRewardsExactlyOnce`.
- Added `SubmitAsyncRejectsSameResultIdWithConflictingPayload`.
- Kept the existing migration smoke test in `MatchResultServiceTests`.
- Ran focused test command from the brief: passed, 4 total tests.
- Ran whitespace check command from the brief: no whitespace errors; Git printed an LF-to-CRLF warning for the edited test file.

## TDD Evidence

### RED Command

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

### RED Output

```text
  Определение проектов для восстановления...
  Все проекты обновлены для восстановления.
  LH.Main.Contracts -> D:\LH_MAIN-phase-02\server\src\LH.Main.Contracts\bin\Release\net10.0\LH.Main.Contracts.dll
  LH.Main.Backend.Api -> D:\LH_MAIN-phase-02\server\src\LH.Main.Backend.Api\bin\Release\net10.0\LH.Main.Backend.Api.dll
D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\MatchResultServiceTests.cs(2,27): error CS0234: Тип или имя пространства имен "MatchResults" не существует в пространстве имен "LH.Main.Backend.Api" (возможно, отсутствует ссылка на сборку). [D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj]
D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\MatchResultServiceTests.cs(109,20): error CS0246: Не удалось найти тип или имя пространства имен "MatchResultService" (возможно, отсутствует директива using или ссылка на сборку). [D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj]
```

### GREEN Command

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

### GREEN Output

```text
  Определение проектов для восстановления...
  Все проекты обновлены для восстановления.
  LH.Main.Contracts -> D:\LH_MAIN-phase-02\server\src\LH.Main.Contracts\bin\Release\net10.0\LH.Main.Contracts.dll
  LH.Main.Backend.Api -> D:\LH_MAIN-phase-02\server\src\LH.Main.Backend.Api\bin\Release\net10.0\LH.Main.Backend.Api.dll
  LH.Main.Backend.Api.Tests -> D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll
Тестовый запуск для D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll (.NETCoreApp,Version=v10.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено     4, пропущено     0, всего     4, длительность 2 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

### Whitespace Check

```powershell
git diff --check -- server/src server/tests
```

```text
warning: in the working copy of 'server/tests/LH.Main.Backend.Api.Tests/MatchResultServiceTests.cs', LF will be replaced by CRLF the next time Git touches it
```

## Files Changed

- `server/src/LH.Main.Backend.Api/MatchResults/MatchResultService.cs`
- `server/tests/LH.Main.Backend.Api.Tests/MatchResultServiceTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-2-report.md`

## Self-Review Findings

- No endpoint mapping was added.
- No wallet/profile balance fields were added.
- No Unity files were changed by this task.
- The implementation is intentionally limited to the service logic requested by Task 2.
- The duplicate path returns `NewRewardTransactions = 0` and does not create additional rows.
- The conflict path does not create additional reward transactions.

## Issues Or Concerns

- Existing unrelated dirty and untracked workspace files were present before this task and were not modified.
- Fix Round 1 below adds explicit recovery for expected unique-constraint failures from concurrent duplicate submissions.

## Fix Round 1

### What Changed

- Added coverage for same `MatchId` submitted with a different `ResultId`.
- Added coverage for concurrent duplicate submissions from separate `AppDbContext` instances.
- Updated `MatchResultService` to check existing `MatchResult` rows by either `ResultId` or `MatchId` before inserting.
- Updated duplicate/conflict resolution so duplicate is returned only when the stored row has the same `ResultId`, `MatchId`, and payload hash; otherwise `result_conflict` is returned.
- Wrapped persistence in expected PostgreSQL unique-constraint handling, clears EF tracked state after the failed save, re-queries the stored result, and returns duplicate or conflict instead of surfacing `DbUpdateException`.

### RED Command

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

### RED Output

```text
Не пройден LH.Main.Backend.Api.Tests.MatchResultServiceTests.SubmitAsyncTreatsConcurrentDuplicateAsDuplicate [217 ms]
Сообщение об ошибке:
 Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.
---- Npgsql.PostgresException : 23505: duplicate key value violates unique constraint "pk_match_results"

Не пройден LH.Main.Backend.Api.Tests.MatchResultServiceTests.SubmitAsyncRejectsSameMatchWithDifferentResultId [78 ms]
Сообщение об ошибке:
 Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.
---- Npgsql.PostgresException : 23505: duplicate key value violates unique constraint "ix_match_results_match_id"

Не пройден!: не пройдено     2, пройдено     4, пропущено     0, всего     6, длительность 2 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

### GREEN Command

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultServiceTests
```

### GREEN Output

```text
  Определение проектов для восстановления...
  Все проекты обновлены для восстановления.
  LH.Main.Contracts -> D:\LH_MAIN-phase-02\server\src\LH.Main.Contracts\bin\Release\net10.0\LH.Main.Contracts.dll
  LH.Main.Backend.Api -> D:\LH_MAIN-phase-02\server\src\LH.Main.Backend.Api\bin\Release\net10.0\LH.Main.Backend.Api.dll
  LH.Main.Backend.Api.Tests -> D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll
Тестовый запуск для D:\LH_MAIN-phase-02\server\tests\LH.Main.Backend.Api.Tests\bin\Release\net10.0\LH.Main.Backend.Api.Tests.dll (.NETCoreApp,Version=v10.0)
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено     6, пропущено     0, всего     6, длительность 2 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

### Whitespace Check

```powershell
git diff --check -- server/src server/tests
```

```text
warning: in the working copy of 'server/src/LH.Main.Backend.Api/MatchResults/MatchResultService.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'server/tests/LH.Main.Backend.Api.Tests/MatchResultServiceTests.cs', LF will be replaced by CRLF the next time Git touches it
```

### Files Changed In Fix Round 1

- `server/src/LH.Main.Backend.Api/MatchResults/MatchResultService.cs`
- `server/tests/LH.Main.Backend.Api.Tests/MatchResultServiceTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-2-report.md`

### Fix Round 1 Self-Review Findings

- No endpoint mapping was added.
- No wallet/profile balance fields were added.
- No Unity files were changed by this fix.
- Unique constraint recovery handles the reviewed duplicate-result and duplicate-match constraints without masking unrelated database errors.

### Fix Round 1 Issues Or Concerns

- Existing unrelated dirty and untracked workspace files remain untouched.
- `git diff --check` still prints LF-to-CRLF warnings, but no whitespace errors.
