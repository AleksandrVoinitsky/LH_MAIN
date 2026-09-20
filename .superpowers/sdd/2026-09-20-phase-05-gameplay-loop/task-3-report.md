# Task 3 Report: Backend Internal Result Endpoint

## What I implemented

- Registered `MatchResultService` in the database-enabled backend service block.
- Added `POST /internal/v1/matches/results` in `Program.cs`.
- Wired the endpoint to `MatchResultService.SubmitAsync(...)` using `X-Game-Server-Key`.
- Mapped endpoint responses per brief:
  - Missing/invalid server key returns `401 Unauthorized`.
  - Conflict returns `409 Conflict` with problem extension `code`.
  - Accepted and duplicate submissions return `200 OK` with the service response.

## What I tested and test results

- Added `MatchResultEndpointTests.cs` covering:
  - Missing server key returns `401`.
  - Valid result submission returns `200`, duplicate same-body submission returns `200` with `"duplicate":true`, and only one reward transaction is persisted.
  - Conflicting result payload returns `409` with `code = "result_conflict"`.
- Focused endpoint verification passed: `3` tests passed, `0` failed.
- Full backend/contract verification passed: `67` total tests passed, `0` failed.
- Whitespace verification: `git diff --check -- server/src server/tests` reported no whitespace errors. It printed only Git's line-ending warning for `server/src/LH.Main.Backend.Api/Program.cs`.

## TDD Evidence

### RED command/output

Command:

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultEndpointTests
```

Output excerpt:

```text
[xUnit.net 00:00:08.09]     LH.Main.Backend.Api.Tests.MatchResultEndpointTests.InternalResultEndpointRejectsMissingServerKey [FAIL]
  Failed LH.Main.Backend.Api.Tests.MatchResultEndpointTests.InternalResultEndpointRejectsMissingServerKey [2 s]
  Error Message:
   Assert.Equal() Failure: Values differ
Expected: Unauthorized
Actual:   NotFound

Failed!: failed 1, passed 0, skipped 0, total 1, duration 2 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

### GREEN command/output

Focused command:

```powershell
dotnet test "server\tests\LH.Main.Backend.Api.Tests\LH.Main.Backend.Api.Tests.csproj" --configuration Release --filter FullyQualifiedName~MatchResultEndpointTests
```

Output excerpt:

```text
Passed!   : failed 0, passed 3, skipped 0, total 3, duration 3 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

Full verification command:

```powershell
dotnet test "server\LH.Main.Server.sln" --configuration Release
```

Output excerpt:

```text
Passed!   : failed 0, passed 1, skipped 0, total 1, duration 13 ms. - LH.Main.Contracts.Tests.dll (net10.0)
Passed!   : failed 0, passed 66, skipped 0, total 66, duration 17 s. - LH.Main.Backend.Api.Tests.dll (net10.0)
```

Whitespace command:

```powershell
git diff --check -- "server/src" "server/tests"
```

Output excerpt:

```text
warning: in the working copy of 'server/src/LH.Main.Backend.Api/Program.cs', LF will be replaced by CRLF the next time Git touches it
```

## Files changed

- `server/src/LH.Main.Backend.Api/Program.cs`
- `server/tests/LH.Main.Backend.Api.Tests/MatchResultEndpointTests.cs`
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-3-report.md`

## Self-review findings

- No blocking findings.
- Endpoint mapping stays inside the existing database-enabled route block, matching the service dependency requirements.
- Tests exercise the real minimal API endpoint through `WebApplicationFactory<Program>` and a PostgreSQL test container.
- The duplicate assertion checks the actual HTTP JSON body and confirms database idempotency via persisted reward transaction count.

## Issues or concerns

- `git diff --check` emitted an existing line-ending warning for `Program.cs`; it did not report whitespace errors.
- The worktree contains unrelated dirty and untracked files outside this task. They were not modified intentionally and should not be included in the Task 3 commit.
