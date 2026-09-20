# Фаза 05: Первый игровой цикл

## Цель

Реализовать минимальный авторитетный цикл: состояние персонажа, урон, тяжёлое ранение, смерть, эвакуация и награда.

## Входит

- Конечный автомат состояний персонажа на сервере.
- Урон и переходы healthy -> wounded -> dead.
- Техническая эвакуационная зона и серверное завершение матча.
- Подписанный результат матча и идемпотентное начисление тестовой награды.

## Не входит

Полный бой, предметы, зона, ИИ, реальный баланс и финальный UI.

## Порядок

1. Формально описать состояния и допустимые переходы до реализации.
2. Написать unit-тесты переходов и интеграционный тест повторной доставки результата.
3. Реализовать server damage/state и сетевое отображение состояния.
4. Реализовать эвакуационный trigger и формирование результата.
5. Проверить сохранение награды один раз при повторной отправке и полный 64-клиентный smoke test.

## Готово, когда

Сервер единолично определяет смерть/эвакуацию, backend сохраняет награду ровно один раз, а клиент не может подменить исход матча.

## Агент может делать без согласования

Использовать технические зоны и заглушки; правила начисления вне тестовой награды не добавляются.

## Проверка Task 7

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-final-editmode.xml" -quit
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -lhClients 64 -lhDurationSeconds 300 -lhPhase05GameplayLoop true -lhReportPath ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-7-final-load-64.json" -quit
```

Task 7 load reports include `extractedClients`, `deadClients`, `disconnectedOutcomeClients`, `resultSubmitted`, `duplicateResultAccepted`, and `rewardTransactions` so the local Phase 05 smoke can capture the technical gameplay-loop outcomes without adding a public reward-count endpoint.

## Проверка Task 8

Финальная проверка выполнена на чистой локальной Compose database после пересборки backend и обоих game-server контейнеров.

```powershell
dotnet test server\LH.Main.Server.sln --configuration Release
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -runTests -testPlatform EditMode -testResults ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-editmode.xml" -quit
docker compose --env-file .env.example --profile game-servers down --volumes
docker compose --env-file .env.example --profile game-servers up --build --detach --wait backend-api game-server-1 game-server-2
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -lhClients 1 -lhDurationSeconds 30 -lhPhase05GameplayLoop true -lhReportPath ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-smoke.json" -quit
& "C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\LH_MAIN-phase-02" -executeMethod LH.Main.Unity.Editor.NetworkedCoreLoadRunner.Run -lhClients 64 -lhDurationSeconds 300 -lhPhase05GameplayLoop true -lhReportPath ".superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-64.json" -quit
```

Unity Editor в этом окружении иногда завершался с `-quit` до обновления XML/JSON, поэтому финальные Unity evidence-файлы были сгенерированы повтором тех же команд без `-quit`.

- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-editmode.xml`: `83/83` EditMode tests passed.
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-smoke.json`: `targetClients=1`, `connectedClients=1`, `spawnedClients=1`, `completedClients=1`, `failedClients=0`, `extractedClients=1`, `deadClients=0`, `disconnectedOutcomeClients=0`, `resultSubmitted=true`, `duplicateResultAccepted=true`, `rewardTransactions=1`.
- `.superpowers/sdd/2026-09-20-phase-05-gameplay-loop/task-8-final-load-64.json`: `targetClients=64`, `connectedClients=64`, `spawnedClients=64`, `completedClients=64`, `failedClients=0`, `extractedClients=22`, `deadClients=21`, `disconnectedOutcomeClients=21`, `resultSubmitted=true`, `duplicateResultAccepted=true`, `rewardTransactions=64`.

Оба load report имеют пустые `disconnectReasons` и `metricCollectionGaps`. `machineNotes` содержит `Phase 05 finalization hook unavailable.` для Editor load-runner, где `ServerBootstrap` отключается при подготовке локального `NetworkManager`; это не блокирует backend submission evidence.
