# Фаза 04: Сетевой фундамент матча

## Цель

Подтвердить FishNet-сценарий с авторитетным спауном и движением до 64 игроков.

## Входит

- Server-owned игрок, server validation ввода/скорости/жизни.
- FishNet observer management и репликация только релевантных объектов.
- Технические prefabs и сценарий 64 воспроизводимых bot/headless клиентов.
- Профилирование и отчёт метрик.

## Не входит

Финальные персонажи, анимации, оружие, ИИ и контентная карта.

## Порядок

1. Определить границы входного RPC и серверную модель состояния.
2. Написать тесты валидатора скорости и недопустимых команд.
3. Реализовать server spawn, disconnect cleanup и movement replication средствами FishNet.
4. Настроить observer management и измерение server tick/трафика.
5. Запустить 64 клиента с одинаковым сценарием, сохранить отчёт и исправлять только измеренные узкие места.

## Готово, когда

64 клиента завершают воспроизводимый сценарий без server error, недопустимые команды отклоняются, а метрики зафиксированы и пригодны для сравнения следующих фаз.

## Verification Snapshot - 2026-09-20

Phase 04 implementation is code-complete through the client/load harness, and the final 64-client runtime verification passed locally.

- Backend automated verification passed: `dotnet test server\LH.Main.Server.sln --configuration Release` completed with `LH.Main.Contracts.Tests` 1/1 and `LH.Main.Backend.Api.Tests` 57/57 tests passing.
- Unity EditMode verification passed: `.superpowers/sdd/2026-09-20-phase-04-networked-core/task-7-final-editmode.xml` completed with 59/59 tests passing.
- Dedicated server build passed when using the full Unity Editor path: `C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe`.
- Linux dedicated server artifact exists at `Builds/GameServer/LinuxHeadless/LH.Main.GameServer.x86_64`.
- Local Compose services started and reported healthy for backend, PostgreSQL, and both game-server containers.
- Backend readiness returned `200` from `http://127.0.0.1:8080/health/ready`; game-server readiness returned `200` from mapped host ports `8091` and `8092`.
- Game-server status returned extended metric fields from `http://127.0.0.1:8091/status`.
- Backend ticket validation smoke passed without printing tickets/shared keys: first validation returned `valid=true`, replay validation returned `valid=false`, and wrong-server validation returned `valid=false`.
- The prior server runtime warning about `GameServerAuthenticator` not being a valid Unity component was fixed by moving `GameServerFishNetAuthenticator` into its own script asset and rebuilding the game-server; the warning is absent from recreated container logs.
- The load runner now loads `Assets/Scenes/Server/ServerBootstrap.unity`, disables the server bootstrap root for client execution, enters PlayMode, and configures FishNet `NetworkManager` persistence to allow multiple local client instances.
- Backend matchmaking now treats one slot as one match with `MaxPlayersPerMatch=64` by default while preserving game-server-authoritative admission/spawn/movement; assignment is protected by a transaction advisory lock to avoid concurrent over/under assignment.
- One-client load smoke passed: `.superpowers/sdd/2026-09-20-phase-04-networked-core/task-7-final-load-smoke.json` recorded `targetClients=1`, `connectedClients=1`, `spawnedClients=1`, `completedClients=1`, `failedClients=0`.
- Final 64-client load passed: `.superpowers/sdd/2026-09-20-phase-04-networked-core/task-7-final-load-64.json` recorded `durationSeconds=300`, `targetClients=64`, `connectedClients=64`, `spawnedClients=64`, `completedClients=64`, `failedClients=0`, backend readiness snapshots with `statusCode=200`, and no disconnect reasons, machine notes, or metric collection gaps.

## Агент может делать без согласования

Использовать штатные возможности FishNet и добавлять нагрузочные инструменты; самописную репликацию создавать нельзя.
