# Phase 03: Match Flow

## Goal

Connect an authenticated client to one reserved dedicated server slot through a backend-owned queue, assignment, and one-time match ticket. The phase turns the two static Phase 02 game-server containers into allocatable local match capacity without adding dynamic orchestration, combat, lobby UI, or production operations.

## Scope

Phase 03 includes backend matchmaking endpoints, PostgreSQL persistence for queue/session state, static game-server slot seeding, transactional slot reservation, short-lived one-time match tickets, and an internal ticket validation endpoint for Unity game-server processes.

Phase 03 does not include dynamic Docker allocation, Kubernetes, production server identity, full lobby UI, real combat, rewards, reconnect policy beyond ticket validation, or result submission.

## Existing Context

The backend is a minimal ASP.NET Core API with JWT authentication, EF Core/Npgsql persistence, development OpenAPI, and integration tests backed by Testcontainers PostgreSQL. Public transport DTOs live in `LH.Main.Contracts` and must remain free of Unity, FishNet, `MonoBehaviour`, `ScriptableObject`, and Unity vector types.

Phase 02 provides static local game-server processes with stable IDs and public network endpoints:

```text
game-server-1 -> localhost:7771
game-server-2 -> localhost:7772
```

The game-server runtime exposes health/status, but Phase 03 allocation is driven by backend database state. Runtime health polling can be added later; this phase only prevents allocating slots that backend already marks unavailable or occupied.

## API Contract

All public matchmaking endpoints require the existing bearer JWT. Endpoints return problem details with stable `code` values for expected failures. Secrets, JWTs, and match tickets must never be logged.

### `POST /v1/matchmaking/queue`

Adds the authenticated player to the queue or returns the player's current active matchmaking state if one already exists.

Response body:

```json
{
  "status": "queued",
  "queuedAtUtc": "2026-09-20T00:00:00Z"
}
```

If capacity is available immediately, the endpoint may reserve a slot and return assignment data:

```json
{
  "status": "assigned",
  "matchId": "00000000-0000-0000-0000-000000000000",
  "serverId": "game-server-1",
  "publicHost": "localhost",
  "publicPort": 7771,
  "ticket": "opaque-random-ticket",
  "ticketExpiresAtUtc": "2026-09-20T00:01:00Z"
}
```

### `GET /v1/matchmaking/status`

Returns the authenticated player's current queue or assignment state. If no active state exists, returns:

```json
{
  "status": "none"
}
```

`assigned` responses contain the same match connection payload as queue assignment. If a ticket has expired before validation, the response returns `expired` and does not expose a replacement ticket; the client must enqueue again.

### `POST /v1/matchmaking/cancel`

Cancels the authenticated player's queued entry when no match has been assigned yet. If the player is already assigned, the endpoint returns a conflict with `code: "match_already_assigned"` and does not free the slot.

Cancellation is idempotent for absent or already-cancelled queue entries and returns the current `none`/`cancelled` state.

### `POST /internal/v1/matches/tickets/validate`

Internal game-server endpoint. It requires `X-Game-Server-Key` to match backend configuration and validates an opaque ticket exactly once.

Request:

```json
{
  "ticket": "opaque-random-ticket",
  "matchId": "00000000-0000-0000-0000-000000000000",
  "playerId": "00000000-0000-0000-0000-000000000000",
  "serverId": "game-server-1"
}
```

Success response:

```json
{
  "valid": true,
  "matchId": "00000000-0000-0000-0000-000000000000",
  "playerId": "00000000-0000-0000-0000-000000000000"
}
```

Failures return `valid: false` for invalid, expired, consumed, wrong-player, wrong-match, or wrong-server tickets. Invalid server key returns `401 Unauthorized` and must not disclose whether the ticket exists.

## Persistence Model

### `game_server_slots`

Static allocatable server capacity seeded from configuration.

Columns:

- `server_id` string primary key, for example `game-server-1`.
- `public_host` string.
- `public_port` integer.
- `status` string: `available`, `occupied`, `disabled`.
- `current_match_id` nullable UUID.
- `created_at_utc` timestamp.
- `updated_at_utc` timestamp.

Only `available` slots can be reserved. `occupied` slots are held until a future lifecycle phase releases them. `disabled` slots are never assigned.

### `match_queue_entries`

Columns:

- `id` UUID primary key.
- `player_id` UUID, FK to `users.id`.
- `status` string: `queued`, `assigned`, `cancelled`, `expired`.
- `created_at_utc`, `updated_at_utc` timestamps.
- `assigned_match_id` nullable UUID.

There is at most one active `queued` or `assigned` entry per player. Repeated queue requests are idempotent and return the active entry instead of creating duplicates.

### `matches`

Columns:

- `id` UUID primary key.
- `player_id` UUID, FK to `users.id`.
- `server_id` string, FK to `game_server_slots.server_id`.
- `status` string: `reserved`, `ticket_validated`, `expired`.
- `created_at_utc`, `updated_at_utc` timestamps.

Phase 03 creates `reserved` matches and moves them to `ticket_validated` when the game-server validates the ticket. Match completion and result submission are outside this phase.

### `match_tickets`

Columns:

- `id` UUID primary key.
- `match_id` UUID, FK to `matches.id`.
- `player_id` UUID.
- `server_id` string.
- `ticket_hash` string unique.
- `expires_at_utc` timestamp.
- `consumed_at_utc` nullable timestamp.
- `created_at_utc` timestamp.

Only a cryptographic hash of the ticket is stored. The plaintext ticket is returned once in assignment responses and is not logged.

## Allocation Flow

When a player enqueues, the backend runs a short transaction:

1. Lock or detect the player's active queue/assignment row.
2. If the player already has an active entry, return it.
3. Insert a queued entry.
4. Find one `available` game-server slot and atomically mark it `occupied`.
5. Create a `reserved` match and a short-lived ticket.
6. Mark the queue entry `assigned`.
7. Commit and return assignment data.

The implementation must use PostgreSQL transaction semantics that prevent two concurrent requests from reserving the same slot. The expected implementation is row-level locking or an atomic conditional update. The tests must prove concurrent allocation does not double-assign one slot.

If no slot is available, the queue entry remains `queued`. In Phase 03, assignment can be attempted again from `GET /status` or a repeated queue request; no background matchmaking worker is required.

## Ticket Validation Flow

The game-server receives a client connection attempt carrying `matchId`, `playerId`, and ticket, then calls the backend internal validation endpoint with `X-Game-Server-Key`.

Backend validation rules:

- The server key must match configured `GameServers:SharedKey`.
- The ticket hash must exist.
- `matchId`, `playerId`, and `serverId` must match the ticket row.
- `expires_at_utc` must be in the future.
- `consumed_at_utc` must be null.

On success, backend sets `consumed_at_utc`, updates the match to `ticket_validated`, and returns `valid: true`. The same ticket must fail on any later validation attempt.

## Configuration

Backend configuration adds:

```text
GameServers:SharedKey
GameServers:TicketLifetimeSeconds
GameServers:Slots:0:ServerId
GameServers:Slots:0:PublicHost
GameServers:Slots:0:PublicPort
GameServers:Slots:1:ServerId
GameServers:Slots:1:PublicHost
GameServers:Slots:1:PublicPort
```

Local `.env.example` maps these to the Phase 02 static Compose services. `GameServers:SharedKey` is a local development secret and must not have a production default.

Startup applies migrations and seeds configured slots idempotently. If a configured slot already exists, host/port/status metadata is updated without deleting current match state.

## OpenAPI and Contracts

Public DTOs are added to `LH.Main.Contracts`:

- matchmaking status response;
- assignment payload;
- cancel response;
- problem codes already remain standard problem details extensions.

Internal ticket validation DTOs may live in the backend project unless Unity client/server code needs compile-time sharing. If shared, they must still remain Unity-free.

Development OpenAPI must describe public authenticated matchmaking endpoints. The internal endpoint may appear in development OpenAPI but must be clearly under `/internal/` and require the server key header.

## Error Handling

Expected public errors:

- `401 Unauthorized` for missing/invalid player JWT.
- `409 Conflict` with `match_already_assigned` when cancelling after assignment.
- `503 Service Unavailable` with `no_match_capacity` only if the queue cannot be persisted or all slots are disabled; normal lack of free slots returns `queued`.

Expected internal errors:

- `401 Unauthorized` for missing/wrong `X-Game-Server-Key`.
- `200 OK` with `valid: false` for invalid ticket material or expired/consumed ticket.

## Observability

Logs include correlation ID, match ID, player ID, and server ID where applicable. Logs must not include plaintext tickets, JWTs, passwords, or shared keys. Public responses may include the ticket only in assignment payloads.

## Testing

Required backend tests:

1. Public matchmaking endpoints require JWT authentication.
2. Queue creates one active queue/assignment per player and repeated calls are idempotent.
3. A free configured slot is assigned with correct `serverId`, `publicHost`, and `publicPort`.
4. Two concurrent enqueue requests cannot receive the same single free slot.
5. When both static slots are occupied, a new player remains `queued`.
6. Cancel removes or marks a queued entry and is idempotent before assignment.
7. Cancel after assignment returns `match_already_assigned` and does not free the slot.
8. Ticket validation requires the shared server key.
9. Ticket validation succeeds once for the correct match/player/server tuple.
10. Reused, expired, wrong-player, wrong-match, and wrong-server tickets return `valid: false`.
11. EF migrations can apply to an empty PostgreSQL database and preserve existing identity data.
12. OpenAPI lists authenticated public matchmaking endpoints.

Manual smoke after Phase 02 runtime is available:

1. Start backend, PostgreSQL, and two game-server containers.
2. Register/login two dev users.
3. Enqueue both users and verify they receive different server slots.
4. Validate each ticket from the assigned `serverId`.
5. Verify replaying a ticket fails.

## Readiness Criteria

- Public matchmaking API is documented and covered by integration tests.
- PostgreSQL migrations create queue, match, ticket, and static server slot tables.
- Slot reservation is transactionally safe under concurrent enqueue requests.
- Match tickets are short-lived, stored hashed, server-bound, player-bound, match-bound, and one-time-use.
- Game-server internal validation is protected by `X-Game-Server-Key`.
- `.env.example` and README document local Phase 03 configuration and smoke commands.
- Existing auth/profile behavior continues to pass.
- `dotnet test`, `docker compose config`, and `git diff --check` pass.

## Risks and Deferrals

- Static shared-key game-server auth is intentionally minimal for local Compose. Production server identity is deferred to operations/security work.
- Backend slot status may diverge from real container health until active health polling or heartbeat is added in a later phase.
- Phase 03 reserves slots but does not implement match completion or slot release. Those lifecycle transitions belong to later gameplay and operations phases.
- The technical client-to-FishNet connection can be completed after ticket validation is available; full gameplay synchronization starts in Phase 04.
