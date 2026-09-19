# Фаза 00: Основа разработки

## Цель

Создать воспроизводимый каркас C#-решения и Docker Compose без реализации игровых правил.

## Входит

- .NET LTS solution: backend API, contracts, domain/application/infrastructure, тестовые проекты.
- Compose с PostgreSQL, пустым backend и двумя именованными слотами game-server.
- Конфигурация окружений, секреты вне Git, health checks, базовый CI.
- Общие transport DTO без Unity-зависимостей.

## Не входит

Аккаунты, миграции предметной схемы, Unity server build, матчмейкинг и игровые механики.

## Порядок

1. Зафиксировать версию .NET LTS и структуру solution в отдельном плане.
2. Создать пустые проекты и unit-тест, проверяющий сборку contracts без Unity references.
3. Добавить Compose, `.env.example`, health checks и отдельные volume/ports для разработки.
4. Поднять только PostgreSQL и backend, проверить `/health` и остановку среды.
5. Описать команды запуска в README и провести review.

## Готово, когда

`docker compose up` поднимает здоровые backend и PostgreSQL; `dotnet test` проходит; contracts не ссылаются на Unity; секреты не попадают в Git.

## Агент может делать без согласования

Создавать каркас solution, тесты, Compose и документацию в границах этой фазы.
