# Фаза 01: Backend и постоянные данные

## Цель

Реализовать минимальную постоянную идентичность игрока и профиль через ASP.NET Core и PostgreSQL: локальная dev-регистрация, вход по логину и паролю, короткоживущий JWT и защищённый endpoint профиля.

## Границы фазы

В фазу входят только `users`, `password_credentials` и `player_profiles`, их миграции и HTTP API идентичности/профиля.

В фазу не входят email, восстановление пароля, подтверждение учётной записи, социальный вход, роли, инвентарь, прогресс, очередь, матчмейкинг, match tickets и Unity-подключение.

## Технологии

- .NET 10 и ASP.NET Core Minimal API.
- Entity Framework Core 10 и Npgsql для PostgreSQL 17.
- `PasswordHasher<TUser>` из `Microsoft.AspNetCore.Identity` для адаптивного PBKDF2-хеша пароля.
- ASP.NET Core JWT Bearer authentication.
- EF Core migrations для единственного способа изменения схемы.
- xUnit и Testcontainers PostgreSQL для интеграционных тестов.

## Модель данных

Схема PostgreSQL использует UUID как идентификатор пользователя и три таблицы:

```text
users
  id uuid primary key
  normalized_username varchar(64) unique not null
  created_at_utc timestamptz not null

password_credentials
  user_id uuid primary key references users(id) on delete cascade
  password_hash text not null

player_profiles
  user_id uuid primary key references users(id) on delete cascade
  username varchar(64) not null
  created_at_utc timestamptz not null
```

`normalized_username` — инвариантный upper-case вариант логина. Он используется только для поиска и уникальности. `username` сохраняет введённое пользователем написание и возвращается в профиле.

Ограничения входных данных:

- логин: от 3 до 64 символов после `Trim()`, только латинские буквы, цифры и `_`;
- пароль: от 12 до 128 символов;
- строки не допускают null, пустое значение и пробелы по краям после нормализации.

Dev-регистрация выполняется одной транзакцией и создаёт все три записи. Повторный нормализованный логин возвращает `409 Conflict` без записи частичного пользователя.

## HTTP API v1

Все новые endpoints находятся под `/v1`. Пароль, JWT и password hash не выводятся в логи, ответы ошибок или OpenAPI-примеры.

### `POST /v1/auth/dev-register`

Принимает:

```json
{
  "username": "dev_player",
  "password": "correct-horse-battery-staple"
}
```

При успехе возвращает `201 Created` и профиль:

```json
{
  "userId": "UUID",
  "username": "dev_player",
  "createdAtUtc": "2026-09-19T00:00:00Z"
}
```

Невалидный запрос возвращает `400 Bad Request` с RFC 9457 problem details. Повторный логин возвращает `409 Conflict` с нейтральным кодом `username_already_exists`.

Endpoint предназначен только для разработки. Его доступ управляется `Authentication:EnableDevRegistration`; значение по умолчанию — `false`. В `Development` конфигурации и локальном Compose он явно включается. Если endpoint выключен, он отвечает `404 Not Found`.

### `POST /v1/auth/dev-login`

Принимает те же `username` и `password`. При успехе возвращает:

```json
{
  "accessToken": "JWT",
  "expiresAtUtc": "2026-09-19T00:15:00Z"
}
```

Для неизвестного логина и неверного пароля возвращает одинаковый `401 Unauthorized` с нейтральным problem code `invalid_credentials`. Время жизни access token по умолчанию — 15 минут и настраивается в `Authentication:AccessTokenLifetimeMinutes`.

JWT содержит `sub` со строковым UUID пользователя, `iss`, `aud`, `iat`, `nbf` и `exp`. Он подписан HMAC SHA-256 ключом из `Authentication:JwtSigningKey`.

### `GET /v1/profile`

Требует `Authorization: Bearer <accessToken>`. Возвращает тот же профиль, что и регистрация. Отсутствующий, некорректный, истёкший token или `sub`, не соответствующий существующему пользователю, возвращает `401 Unauthorized`.

## Конфигурация и запуск

Backend читает следующие переменные окружения:

```text
ConnectionStrings__MainDb
Authentication__JwtSigningKey
Authentication__Issuer
Authentication__Audience
Authentication__AccessTokenLifetimeMinutes
Authentication__EnableDevRegistration
```

`JwtSigningKey`, `Issuer` и `Audience` обязательны. Production-значения не имеют значений по умолчанию в коде или Compose. `.env.example` содержит только безопасные локальные примеры и не является production-конфигурацией.

При запуске backend подключается к PostgreSQL и применяет ожидающие migrations до запуска HTTP listener. При ошибке миграции процесс завершает работу с ненулевым кодом, не открывая API.

## Health, наблюдаемость и OpenAPI

- `/health/live` остаётся проверкой живости процесса и не использует БД.
- `/health/ready` проверяет соединение с PostgreSQL и возвращает `503 Service Unavailable`, пока БД недоступна.
- Middleware берёт `X-Correlation-ID`, если это непустой корректный UUID; иначе создаёт UUID. Идентификатор записывается в `HttpContext.TraceIdentifier`, structured logs и ответный header `X-Correlation-ID`.
- OpenAPI публикуется только в `Development`, описывает v1 endpoints и bearer security scheme.

## Тестирование

Каждая новая функция создаётся через TDD: сначала конкретный тест и наблюдаемый RED, затем минимальная реализация и GREEN.

Интеграционные тесты используют отдельный PostgreSQL Testcontainer. Они проверяют:

1. migrations на пустой БД и повторный запуск migration без потери записей;
2. успешную dev-регистрацию, создание credentials/profile и уникальность нормализованного логина;
3. неверный пароль и неизвестный пользователь с одинаковым `401 invalid_credentials`;
4. успешный login и JWT со сроком действия;
5. отказ для истёкшего JWT и успешный вызов профиля с действующим JWT;
6. `401` профиля для отсутствующего/некорректного token и несуществующего `sub`;
7. `503` readiness при недоступной БД и `200` при рабочей;
8. распространение или генерацию correlation ID без записи пароля/JWT в тестируемых логах.

## Критерии готовности

- Пустая PostgreSQL получает схему только через EF migration; повторный запуск безопасен.
- Локальный пользователь проходит `dev-register` -> `dev-login` -> `GET /v1/profile`.
- Пароль хранится только как PBKDF2 hash; неизвестный логин и неверный пароль не различаются внешним API.
- JWT и обязательная конфигурация не имеют production default в Git.
- Contracts не получают ссылок на Unity, FishNet или `UnityEngine`.
- `dotnet test server/LH.Main.Server.sln --configuration Release`, release build и `docker compose config` проходят.
- При доступном Docker daemon Compose успешно поднимает PostgreSQL/backend, migrations применяются, `/health/ready` возвращает `200`.
