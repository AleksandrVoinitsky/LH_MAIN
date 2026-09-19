# Phase 04: Networked Core Design

## Goal

Prove the Unity/FishNet match foundation with backend-issued assignment tickets, authoritative server admission, server-owned player spawn, validated movement, FishNet observer management, and a reproducible 64-client bot/headless load scenario.

## Scope

Phase 04 includes the minimal client-to-match connection path, game-server ticket validation against the Phase 03 backend endpoint, server-owned technical player objects, movement intent validation, disconnect cleanup, observer management, game-server metrics, and load-smoke documentation for 64 clients.

Phase 04 does not include final characters, animation, weapons, combat, damage, loot, inventory, AI, content maps, match result submission, rewards, reconnect policy, dynamic game-server allocation, custom transport, custom snapshotting, or custom replication.

## Existing Context

The backend already owns authentication, queue state, static game-server slots, match sessions, and one-time ticket validation. `POST /internal/v1/matches/tickets/validate` accepts `TicketValidationRequest` and returns `TicketValidationResponse`; it requires `X-Game-Server-Key`, consumes valid tickets once, and rejects expired, reused, wrong-player, wrong-match, or wrong-server tickets.

The Unity dedicated server already starts FishNet/Tugboat from `ServerBootstrap.unity`, reads local server identity and ports from environment variables, and exposes `/health/ready` plus `/status`. Phase 04 builds on that server bootstrap instead of replacing it.

FishNet remains the required transport, RPC, replication, observer management, and dedicated-server networking layer. No custom transport or replication protocol may be added unless FishNet limitation is measured and documented.

## Match Admission Flow

The client obtains a Phase 03 matchmaking assignment through the backend public API. The assignment includes `matchId`, `serverId`, `publicHost`, `publicPort`, `ticket`, and `ticketExpiresAtUtc`.

Before opening the FishNet connection, the client builds a connection payload:

```json
{
  "matchId": "00000000-0000-0000-0000-000000000000",
  "playerId": "00000000-0000-0000-0000-000000000000",
  "serverId": "game-server-1",
  "ticket": "opaque-random-ticket"
}
```

The player ID comes from the authenticated profile or JWT-backed profile response, not from a Unity-only local identity. The payload is sent through FishNet's authentication or connection-admission extension point for the transport version installed in the project.

On the game-server, the admission component parses the payload, checks that `serverId` equals the local `GAME_SERVER_ID`, and calls the backend validation endpoint using the configured shared key. Admission succeeds only when the backend returns `valid: true` for the same `matchId` and `playerId`. Failed admission rejects the connection without logging the plaintext ticket.

## Server Configuration

Game-server configuration adds backend validation settings:

```text
GAME_SERVER_BACKEND_BASE_URL
GAME_SERVER_SHARED_KEY
GAME_SERVER_TICKET_VALIDATION_TIMEOUT_SECONDS
```

The existing variables remain required:

```text
GAME_SERVER_ID
GAME_SERVER_HTTP_PORT
GAME_SERVER_NETWORK_PORT
GAME_SERVER_PUBLIC_HOST
GAME_SERVER_PUBLIC_NETWORK_PORT
```

The server must fail startup when required admission settings are absent in dedicated-server mode. Development scenes may provide inspector defaults only when they do not hide missing container/runtime configuration.

## Unity Runtime Components

`MatchAdmissionPayload` is a Unity-side DTO with string/Guid fields only. It has no dependency on UnityEngine or FishNet so that parsing and validation can be tested outside the scene.

`MatchTicketValidator` owns the HTTP call to the backend internal endpoint. It sends `X-Game-Server-Key`, serializes the Phase 03 validation request, applies a short timeout, returns an explicit success/failure result, and never logs the ticket or shared key.

`GameServerAuthenticator` owns FishNet connection admission. It decodes the payload, checks the local server ID, calls `MatchTicketValidator`, records metrics, accepts valid connections, rejects invalid connections, and exposes the validated `matchId` and `playerId` to spawning code.

`ServerPlayerRegistry` maps accepted FishNet connections to player state. It creates one server-owned player per validated connection, prevents duplicate spawn for the same connection, rejects spawn for unauthenticated connections, and removes state on disconnect.

`NetworkPlayerController` is the technical networked player object. The server owns authoritative position, rotation, velocity, the minimal life state required for this phase, and validation counters. Clients send input intent only.

## Player Spawn

After successful admission, the server spawns a technical player prefab through FishNet server spawn APIs and associates it with the accepted connection. The client does not spawn its own player object. The spawned object contains only technical visuals/collider/controller pieces required to verify networking.

Spawn state includes:

- `matchId`
- `playerId`
- FishNet connection ID
- spawn time UTC or monotonic server time
- latest accepted input sequence
- authoritative position and velocity
- life state: `alive` or `disconnected`

On disconnect, the server despawns the player's network object, removes registry state, increments disconnect metrics, and keeps the game-server process healthy unless a fatal server error occurred.

## Movement Validation

Clients send input intent, not position authority. The minimal command is:

```text
sequence: uint
moveX: float, range -1..1
moveY: float, range -1..1
sentAtClientTime: double
```

The server normalizes movement intent, rejects non-finite values, ignores duplicate or old sequence numbers, rate-limits command processing per connection, and applies movement using server delta time. A command is invalid when it would exceed configured max speed, max acceleration, or command rate.

Default technical values:

```text
MaxSpeedMetersPerSecond = 6
MaxAccelerationMetersPerSecondSquared = 24
MaxCommandsPerSecond = 30
```

Invalid commands are rejected or clamped consistently and counted in metrics. The server-replicated state is the only source of truth for visible player position.

## Observer Management

Phase 04 uses FishNet observer management for technical player objects. The first implementation may use a simple distance-based or scene-wide observer policy if that is the smallest FishNet-native path to a stable 64-client smoke test. It must not add custom replication, custom snapshots, or hand-written network serialization outside FishNet's supported component model.

The load report records the chosen observer policy and the number of observed player objects per client when that metric is practical to collect.

## Metrics And Diagnostics

The game-server `/status` response is extended with operational counters:

```json
{
  "serverId": "game-server-1",
  "state": "idle",
  "networkPort": 7771,
  "publicHost": "localhost",
  "publicNetworkPort": 7771,
  "startedAtUtc": "2026-09-20T00:00:00.0000000Z",
  "activeConnections": 64,
  "spawnedPlayers": 64,
  "acceptedAdmissions": 64,
  "rejectedAdmissions": 0,
  "invalidInputCommands": 0,
  "disconnects": 0,
  "serverTickRate": 60,
  "serverTickP95Ms": 8.4,
  "processMemoryMb": 512,
  "inboundKbps": 320,
  "outboundKbps": 960
}
```

If a metric cannot be collected from FishNet or Unity APIs in this phase, the report must explicitly mark it as `not_collected` and state why. Secrets are never logged: passwords, JWTs, plaintext tickets, and shared keys are forbidden in logs and generated reports.

## Load Scenario

The 64-client scenario is deterministic:

1. Start backend, PostgreSQL, and one game-server slot.
2. Create or reuse 64 development users.
3. Queue users until they receive assignments for the target server and match.
4. Launch 64 bot/headless clients with unique player credentials or validated assignment payloads.
5. Each client connects, completes admission, waits for spawn, follows a deterministic movement route for a fixed duration, and disconnects cleanly.
6. Save a report containing scenario parameters, machine context, server status snapshots, pass/fail counts, tick metrics, memory, traffic, disconnect reasons, and error logs summary.

The preferred duration for Phase 04 smoke is 5 minutes after all 64 clients are connected. A shorter duration is allowed only for local development while iterating and cannot satisfy the phase definition of done.

## Error Handling

Admission rejects these cases:

- malformed payload
- missing ticket
- wrong local server ID
- backend timeout
- backend `401` from invalid `X-Game-Server-Key`
- backend `valid: false`
- mismatch between validation response and requested match/player

The client surfaces a generic connection failure in this phase. Detailed UX copy is out of scope; diagnostics go to development logs without exposing secrets.

Movement rejects these cases:

- command before admission or spawn
- non-finite axis values
- axis values outside `-1..1`
- old or duplicate sequence number
- excessive command rate
- speed or acceleration beyond server limits

## Testing

Required automated tests where Unity runtime allows edit-mode coverage:

1. Admission payload parsing accepts valid JSON and rejects malformed/empty payloads.
2. Server ID mismatch fails before backend validation.
3. Ticket validator sends the shared-key header and never includes the ticket in log messages.
4. Ticket validator maps `valid: true`, `valid: false`, `401`, and timeout to explicit outcomes.
5. Movement validator accepts normal input, rejects non-finite axes, rejects duplicate sequence, and clamps or rejects speed violations.
6. Player registry spawns only after validated admission and cleans up on disconnect.
7. Status JSON includes metrics fields and escapes strings safely.

Required manual or scripted verification:

1. Backend tests still pass.
2. Unity dedicated-server build succeeds.
3. One client obtains assignment, connects to the assigned server, validates ticket, spawns, moves, and disconnects.
4. Reusing the same ticket fails admission.
5. A wrong-server payload fails admission.
6. 64 bot/headless clients complete the deterministic scenario and produce the metrics report.

## Definition Of Done

Phase 04 is complete when 64 clients finish the deterministic scenario without server errors, every spawned player came from a validated one-time ticket, replayed/invalid/wrong-server tickets cannot enter the match, server authority controls spawn and movement state, invalid movement commands are rejected or clamped and counted, metrics are recorded for comparison in later phases, and no forbidden secret appears in logs or reports.
