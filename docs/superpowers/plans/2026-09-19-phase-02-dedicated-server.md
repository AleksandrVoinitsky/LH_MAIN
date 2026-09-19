# Phase 02 Dedicated Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reproducible local contour with two pre-started Linux headless Unity/FishNet game-server containers that expose health/status and are ready for Phase 03 allocation.

**Architecture:** Add a small Unity server bootstrap layer under `Assets/Scripts/Server/` plus a dedicated technical scene under `Assets/Scenes/Server/`. The Unity process starts FishNet server mode, owns a minimal HTTP listener for `/health/ready` and `/status`, and is packaged into a Docker runtime image from a prebuilt Linux headless player. Compose reuses the existing `game-server-1` and `game-server-2` services and completes them with ports, env, and healthchecks.

**Tech Stack:** Unity 6000.3.14f1, FishNet 4.7.3 Tugboat transport, C# `HttpListener`, Unity Editor build script, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-19-phase-02-dedicated-server-design.md`

## Global Constraints

- Do not start Phase 03: no queue, match tickets, backend allocator, client match connection, player character, combat, map, inventory, rewards, PvE, final UI, or production orchestration.
- Do not modify user local Unity/IDE files unless a task explicitly requires that exact file: `Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/ProjectSettings.asset`, `.vsconfig`, `Assets/DefaultPrefabObjects.asset`, `Assets/DefaultPrefabObjects.asset.meta`, `harvicode.json`.
- Use Unity 6000.3.14f1 and FishNet 4.7.3.
- Use FishNet as the only network foundation; do not create a custom transport, snapshot protocol, or replication system.
- Docker image must not contain Unity Editor, Unity license/activation, `.env`, `Library`, `Temp`, IDE artifacts, or secrets.
- Runtime config comes from environment variables: `GAME_SERVER_ID`, `GAME_SERVER_HTTP_PORT`, `GAME_SERVER_NETWORK_PORT`, `GAME_SERVER_PUBLIC_HOST`, `GAME_SERVER_PUBLIC_NETWORK_PORT`.
- Phase 02 implements `idle` and `failed`; `reserved` and `running` are contract values only.
- Run `git status --short --branch` before editing and do not revert unrelated user changes.

---

## File Structure

- Create `Assets/Scripts/Server/GameServerState.cs`: enum with `Idle`, `Reserved`, `Running`, `Failed` and JSON string conversion helper.
- Create `Assets/Scripts/Server/GameServerConfig.cs`: reads and validates environment variables without Unity scene dependencies.
- Create `Assets/Scripts/Server/GameServerStatus.cs`: immutable status payload for `/status`.
- Create `Assets/Scripts/Server/GameServerHealthServer.cs`: owns `HttpListener`, response serialization, background request loop, and shutdown.
- Create `Assets/Scripts/Server/GameServerBootstrap.cs`: Unity `MonoBehaviour` that wires config, FishNet `NetworkManager`, Tugboat port, server start, health listener, state transitions, and shutdown.
- Create `Assets/Scripts/Editor/GameServerBuild.cs`: Unity Editor build entry point for Linux headless/server build.
- Create `Assets/Scenes/Server/ServerBootstrap.unity`: technical scene containing `GameServerBootstrap`, FishNet `NetworkManager`, and Tugboat transport.
- Modify `ProjectSettings/EditorBuildSettings.asset`: include `Assets/Scenes/Server/ServerBootstrap.unity` as the server build scene if Unity creates or updates it during scene setup.
- Create `docker/game-server/Dockerfile`: runtime image that copies `Builds/GameServer/LinuxHeadless/` output.
- Create `docker/game-server/entrypoint.sh`: validates executable path and starts the Unity player.
- Modify `.dockerignore`: exclude Unity generated folders and include enough build output for `docker/game-server/Dockerfile`.
- Modify `docker-compose.yml`: complete existing `game-server-1` and `game-server-2` services with env, ports, healthchecks, and profile.
- Modify `.env.example`: add local game-server host/container ports.
- Modify `README.md`: add Phase 02 build/run/check commands.

---

### Task 1: Server config, status, and HTTP health unit

**Files:**
- Create: `Assets/Scripts/Server/GameServerState.cs`
- Create: `Assets/Scripts/Server/GameServerConfig.cs`
- Create: `Assets/Scripts/Server/GameServerStatus.cs`
- Create: `Assets/Scripts/Server/GameServerHealthServer.cs`

**Interfaces:**
- Produces: `GameServerConfig.ReadFromEnvironment() : GameServerConfig`
- Produces: `GameServerConfig.Validate(out string error) : bool`
- Produces: `GameServerStateJson.ToJsonValue(GameServerState state) : string`
- Produces: `GameServerStatus.ToJson() : string`
- Produces: `GameServerHealthServer.Start(GameServerConfig config, Func<GameServerStatus> statusProvider, Func<bool> readyProvider) : void`
- Produces: `GameServerHealthServer.Stop() : void`

- [ ] **Step 1: Read the current git state**

Run: `git status --short --branch`

Expected: only the known user Unity/IDE files plus Phase 02 docs are dirty before implementation starts. If additional unrelated files are dirty, leave them untouched.

- [ ] **Step 2: Create `GameServerState.cs`**

Add:

```csharp
namespace LH.Main.Unity.Server;

public enum GameServerState
{
    Idle,
    Reserved,
    Running,
    Failed
}

public static class GameServerStateJson
{
    public static string ToJsonValue(GameServerState state)
    {
        return state switch
        {
            GameServerState.Idle => "idle",
            GameServerState.Reserved => "reserved",
            GameServerState.Running => "running",
            GameServerState.Failed => "failed",
            _ => "failed"
        };
    }
}
```

- [ ] **Step 3: Create `GameServerConfig.cs`**

Add:

```csharp
using System;

namespace LH.Main.Unity.Server;

public sealed class GameServerConfig
{
    public const string ServerIdVariable = "GAME_SERVER_ID";
    public const string HttpPortVariable = "GAME_SERVER_HTTP_PORT";
    public const string NetworkPortVariable = "GAME_SERVER_NETWORK_PORT";
    public const string PublicHostVariable = "GAME_SERVER_PUBLIC_HOST";
    public const string PublicNetworkPortVariable = "GAME_SERVER_PUBLIC_NETWORK_PORT";

    public string ServerId { get; }
    public ushort HttpPort { get; }
    public ushort NetworkPort { get; }
    public string PublicHost { get; }
    public ushort PublicNetworkPort { get; }

    public GameServerConfig(string serverId, ushort httpPort, ushort networkPort, string publicHost, ushort publicNetworkPort)
    {
        ServerId = serverId;
        HttpPort = httpPort;
        NetworkPort = networkPort;
        PublicHost = publicHost;
        PublicNetworkPort = publicNetworkPort;
    }

    public static GameServerConfig ReadFromEnvironment()
    {
        return new GameServerConfig(
            Environment.GetEnvironmentVariable(ServerIdVariable) ?? string.Empty,
            ReadPort(HttpPortVariable),
            ReadPort(NetworkPortVariable),
            Environment.GetEnvironmentVariable(PublicHostVariable) ?? string.Empty,
            ReadPort(PublicNetworkPortVariable));
    }

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ServerId))
        {
            error = $"{ServerIdVariable} is required.";
            return false;
        }

        if (HttpPort == 0)
        {
            error = $"{HttpPortVariable} must be a valid TCP port from 1 to 65535.";
            return false;
        }

        if (NetworkPort == 0)
        {
            error = $"{NetworkPortVariable} must be a valid UDP port from 1 to 65535.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PublicHost))
        {
            error = $"{PublicHostVariable} is required.";
            return false;
        }

        if (PublicNetworkPort == 0)
        {
            error = $"{PublicNetworkPortVariable} must be a valid UDP port from 1 to 65535.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static ushort ReadPort(string variableName)
    {
        string? raw = Environment.GetEnvironmentVariable(variableName);
        if (!ushort.TryParse(raw, out ushort port))
            return 0;

        return port;
    }
}
```

- [ ] **Step 4: Create `GameServerStatus.cs`**

Add:

```csharp
using System;

namespace LH.Main.Unity.Server;

public sealed class GameServerStatus
{
    public string ServerId { get; }
    public GameServerState State { get; }
    public ushort NetworkPort { get; }
    public string PublicHost { get; }
    public ushort PublicNetworkPort { get; }
    public DateTime StartedAtUtc { get; }

    public GameServerStatus(
        string serverId,
        GameServerState state,
        ushort networkPort,
        string publicHost,
        ushort publicNetworkPort,
        DateTime startedAtUtc)
    {
        ServerId = serverId;
        State = state;
        NetworkPort = networkPort;
        PublicHost = publicHost;
        PublicNetworkPort = publicNetworkPort;
        StartedAtUtc = startedAtUtc;
    }

    public string ToJson()
    {
        return "{"
            + $"\"serverId\":\"{Escape(ServerId)}\","
            + $"\"state\":\"{GameServerStateJson.ToJsonValue(State)}\","
            + $"\"networkPort\":{NetworkPort},"
            + $"\"publicHost\":\"{Escape(PublicHost)}\","
            + $"\"publicNetworkPort\":{PublicNetworkPort},"
            + $"\"startedAtUtc\":\"{StartedAtUtc:O}\""
            + "}";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
```

- [ ] **Step 5: Create `GameServerHealthServer.cs`**

Add:

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LH.Main.Unity.Server;

public sealed class GameServerHealthServer
{
    private HttpListener? _listener;
    private CancellationTokenSource? _stopping;
    private Task? _requestLoop;
    private Func<GameServerStatus>? _statusProvider;
    private Func<bool>? _readyProvider;

    public void Start(GameServerConfig config, Func<GameServerStatus> statusProvider, Func<bool> readyProvider)
    {
        if (_listener != null)
            throw new InvalidOperationException("Health server is already running.");

        _statusProvider = statusProvider;
        _readyProvider = readyProvider;
        _stopping = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{config.HttpPort}/");
        _listener.Start();
        _requestLoop = Task.Run(() => RunAsync(_stopping.Token));
    }

    public void Stop()
    {
        CancellationTokenSource? stopping = _stopping;
        HttpListener? listener = _listener;

        _stopping = null;
        _listener = null;
        _statusProvider = null;
        _readyProvider = null;

        try
        {
            stopping?.Cancel();
            listener?.Stop();
            listener?.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            stopping?.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                HttpListener listener = _listener ?? return;
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context), stoppingToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        string path = context.Request.Url?.AbsolutePath ?? string.Empty;

        if (path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase))
        {
            bool ready = _readyProvider?.Invoke() == true;
            string body = ready ? "{\"status\":\"ok\"}" : "{\"status\":\"not_ready\"}";
            await WriteJsonAsync(context, ready ? 200 : 503, body).ConfigureAwait(false);
            return;
        }

        if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            GameServerStatus? status = _statusProvider?.Invoke();
            await WriteJsonAsync(context, 200, status?.ToJson() ?? "{\"state\":\"failed\"}").ConfigureAwait(false);
            return;
        }

        await WriteJsonAsync(context, 404, "{\"error\":\"not_found\"}").ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;

        try
        {
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
        catch (HttpListenerException ex)
        {
            Debug.LogWarning($"Health response write failed: {ex.Message}");
        }
        finally
        {
            context.Response.Close();
        }
    }
}
```

- [ ] **Step 6: Compile scripts in Unity**

Open Unity 6000.3.14f1 and wait for script compilation.

Expected: Unity Console has no C# compile errors from `Assets/Scripts/Server/*`.

- [ ] **Step 7: Commit Task 1**

Run:

```bash
git add Assets/Scripts/Server/GameServerState.cs Assets/Scripts/Server/GameServerConfig.cs Assets/Scripts/Server/GameServerStatus.cs Assets/Scripts/Server/GameServerHealthServer.cs
git commit -m "feat: add game server health primitives"
```

---

### Task 2: Unity FishNet bootstrap scene and build script

**Files:**
- Create: `Assets/Scripts/Server/GameServerBootstrap.cs`
- Create: `Assets/Scripts/Editor/GameServerBuild.cs`
- Create: `Assets/Scenes/Server/ServerBootstrap.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset` if Unity updates build scene settings

**Interfaces:**
- Consumes: `GameServerConfig`, `GameServerHealthServer`, `GameServerStatus`, `GameServerState`
- Produces: scene `Assets/Scenes/Server/ServerBootstrap.unity`
- Produces: build method `LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless()`

- [ ] **Step 1: Create `GameServerBootstrap.cs`**

Add:

```csharp
using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace LH.Main.Unity.Server;

public sealed class GameServerBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager? _networkManager;
    [SerializeField] private Tugboat? _transport;

    private readonly GameServerHealthServer _healthServer = new();
    private GameServerConfig? _config;
    private GameServerState _state = GameServerState.Failed;
    private DateTime _startedAtUtc;

    private void Awake()
    {
        _startedAtUtc = DateTime.UtcNow;
        _config = GameServerConfig.ReadFromEnvironment();

        if (!_config.Validate(out string error))
        {
            _state = GameServerState.Failed;
            Debug.LogError($"Game server config invalid: {error}");
            Application.Quit(1);
            return;
        }

        if (_networkManager == null)
            _networkManager = FindFirstObjectByType<NetworkManager>();

        if (_transport == null)
            _transport = FindFirstObjectByType<Tugboat>();

        if (_networkManager == null || _transport == null)
        {
            _state = GameServerState.Failed;
            Debug.LogError("FishNet NetworkManager and Tugboat transport are required in the server scene.");
            Application.Quit(1);
            return;
        }

        _transport.SetPort(_config.NetworkPort);
        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

        try
        {
            _healthServer.Start(_config, CreateStatus, IsReady);
        }
        catch (Exception ex)
        {
            _state = GameServerState.Failed;
            Debug.LogError($"Health listener failed to start: {ex}");
            Application.Quit(1);
            return;
        }

        bool started = _networkManager.ServerManager.StartConnection();
        if (!started)
        {
            _state = GameServerState.Failed;
            Debug.LogError("FishNet server failed to start.");
            Application.Quit(1);
            return;
        }

        Debug.Log($"Game server {_config.ServerId} starting on network port {_config.NetworkPort} and health port {_config.HttpPort}.");
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;

        _healthServer.Stop();
    }

    private void OnApplicationQuit()
    {
        _healthServer.Stop();
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _state = GameServerState.Idle;
            Debug.Log($"Game server {_config?.ServerId} is idle.");
            return;
        }

        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _state = GameServerState.Failed;
            Debug.LogWarning($"Game server {_config?.ServerId} stopped.");
        }
    }

    private bool IsReady()
    {
        return _state == GameServerState.Idle;
    }

    private GameServerStatus CreateStatus()
    {
        GameServerConfig config = _config ?? new GameServerConfig("unknown", 0, 0, "unknown", 0);
        return new GameServerStatus(
            config.ServerId,
            _state,
            config.NetworkPort,
            config.PublicHost,
            config.PublicNetworkPort,
            _startedAtUtc);
    }
}
```

- [ ] **Step 2: Create `GameServerBuild.cs`**

Add:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LH.Main.Unity.Editor;

public static class GameServerBuild
{
    private const string ScenePath = "Assets/Scenes/Server/ServerBootstrap.unity";
    private const string OutputDirectory = "Builds/GameServer/LinuxHeadless";
    private const string OutputPath = OutputDirectory + "/LH.Main.GameServer.x86_64";

    [MenuItem("LH Main/Build/Linux Headless Game Server")]
    public static void BuildLinuxHeadless()
    {
        Directory.CreateDirectory(OutputDirectory);

        BuildPlayerOptions options = new()
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.EnableHeadlessMode
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Game server build failed: {report.summary.result}");
    }
}
```

- [ ] **Step 3: Create the technical server scene in Unity**

In Unity 6000.3.14f1:

1. Create folder `Assets/Scenes/Server` if it does not exist.
2. Create scene `Assets/Scenes/Server/ServerBootstrap.unity`.
3. Add an empty GameObject named `ServerBootstrap`.
4. Add `GameServerBootstrap` to `ServerBootstrap`.
5. Add FishNet `NetworkManager` object using FishNet's menu or by creating a GameObject with `NetworkManager` and required FishNet manager components.
6. Add `Tugboat` transport to the NetworkManager object if FishNet did not add it automatically.
7. Wire `GameServerBootstrap._networkManager` to the scene `NetworkManager` and `_transport` to the scene `Tugboat` in the Inspector.
8. Save the scene.

Expected: the scene has no camera/UI dependencies and only technical server objects.

- [ ] **Step 4: Add server scene to build settings if Unity requires it**

Open `File > Build Profiles` or `File > Build Settings` and ensure `Assets/Scenes/Server/ServerBootstrap.unity` is the only enabled scene needed for the server build profile.

Expected: Unity may update `ProjectSettings/EditorBuildSettings.asset`; inspect the diff and keep only the server scene setting if this file changes.

- [ ] **Step 5: Compile and build the Linux headless player**

Run from a terminal with Unity available on PATH or use the full Unity Editor path:

```bash
Unity -batchmode -quit -projectPath . -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless
```

Expected: `Builds/GameServer/LinuxHeadless/LH.Main.GameServer.x86_64` exists and Unity exits with code 0.

- [ ] **Step 6: Confirm build output is ignored by Git**

Run: `git status --short Builds`

Expected: no tracked or untracked files are reported under `Builds/`.

- [ ] **Step 7: Commit Task 2**

Run:

```bash
git add Assets/Scripts/Server/GameServerBootstrap.cs Assets/Scripts/Editor/GameServerBuild.cs Assets/Scenes/Server/ServerBootstrap.unity Assets/Scenes/Server/ServerBootstrap.unity.meta Assets/Scenes/Server.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: add unity dedicated server bootstrap"
```

If `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scenes/Server.meta`, or scene `.meta` files were not created or modified, omit the absent paths from `git add`.

---

### Task 3: Game-server runtime Docker image

**Files:**
- Create: `docker/game-server/Dockerfile`
- Create: `docker/game-server/entrypoint.sh`
- Modify: `.dockerignore`

**Interfaces:**
- Consumes: build output `Builds/GameServer/LinuxHeadless/LH.Main.GameServer.x86_64`
- Produces: Docker image build target used by `docker-compose.yml` services

- [ ] **Step 1: Create `docker/game-server/Dockerfile`**

Add:

```dockerfile
FROM ubuntu:24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libatomic1 libpulse0 libstdc++6 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY Builds/GameServer/LinuxHeadless/ /app/
COPY docker/game-server/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh /app/LH.Main.GameServer.x86_64

EXPOSE 8081/tcp
EXPOSE 7770/udp

ENTRYPOINT ["/entrypoint.sh"]
```

- [ ] **Step 2: Create `docker/game-server/entrypoint.sh`**

Add with LF line endings:

```sh
#!/usr/bin/env sh
set -eu

executable="/app/LH.Main.GameServer.x86_64"

if [ ! -x "$executable" ]; then
  echo "Game server executable not found or not executable: $executable" >&2
  exit 1
fi

exec "$executable" -batchmode -nographics -logfile -
```

- [ ] **Step 3: Update `.dockerignore`**

Ensure it contains:

```text
**/bin/
**/obj/
.git/
.env
Library/
Temp/
Obj/
Logs/
UserSettings/
MemoryCaptures/
Builds/**
!Builds/GameServer/
!Builds/GameServer/LinuxHeadless/
!Builds/GameServer/LinuxHeadless/**
```

- [ ] **Step 4: Build the Docker image**

Run after Task 2 produced the Unity build:

```bash
docker build -f docker/game-server/Dockerfile -t lh-main-game-server:local .
```

Expected: Docker build succeeds and does not copy Unity `Library` or `Temp` into the build context.

- [ ] **Step 5: Commit Task 3**

Run:

```bash
git add docker/game-server/Dockerfile docker/game-server/entrypoint.sh .dockerignore
git commit -m "feat: add game server runtime image"
```

---

### Task 4: Compose profile for two game-server containers

**Files:**
- Modify: `docker-compose.yml`
- Modify: `.env.example`

**Interfaces:**
- Consumes: Dockerfile from Task 3
- Produces: Compose services `game-server-1` and `game-server-2` with healthchecks

- [ ] **Step 1: Update `.env.example`**

Append:

```text
GAME_SERVER_PUBLIC_HOST=localhost
GAME_SERVER_HTTP_CONTAINER_PORT=8081
GAME_SERVER_NETWORK_CONTAINER_PORT=7770
GAME_SERVER_1_HTTP_HOST_PORT=8091
GAME_SERVER_1_NETWORK_HOST_PORT=7771
GAME_SERVER_2_HTTP_HOST_PORT=8092
GAME_SERVER_2_NETWORK_HOST_PORT=7772
```

- [ ] **Step 2: Complete `game-server-1` in `docker-compose.yml`**

Replace the current `game-server-1` service body with:

```yaml
  game-server-1:
    profiles: ["game-servers"]
    build:
      context: .
      dockerfile: docker/game-server/Dockerfile
    environment:
      GAME_SERVER_ID: game-server-1
      GAME_SERVER_HTTP_PORT: ${GAME_SERVER_HTTP_CONTAINER_PORT:?Set GAME_SERVER_HTTP_CONTAINER_PORT in .env}
      GAME_SERVER_NETWORK_PORT: ${GAME_SERVER_NETWORK_CONTAINER_PORT:?Set GAME_SERVER_NETWORK_CONTAINER_PORT in .env}
      GAME_SERVER_PUBLIC_HOST: ${GAME_SERVER_PUBLIC_HOST:?Set GAME_SERVER_PUBLIC_HOST in .env}
      GAME_SERVER_PUBLIC_NETWORK_PORT: ${GAME_SERVER_1_NETWORK_HOST_PORT:?Set GAME_SERVER_1_NETWORK_HOST_PORT in .env}
    ports:
      - "127.0.0.1:${GAME_SERVER_1_HTTP_HOST_PORT:?Set GAME_SERVER_1_HTTP_HOST_PORT in .env}:${GAME_SERVER_HTTP_CONTAINER_PORT:?Set GAME_SERVER_HTTP_CONTAINER_PORT in .env}/tcp"
      - "127.0.0.1:${GAME_SERVER_1_NETWORK_HOST_PORT:?Set GAME_SERVER_1_NETWORK_HOST_PORT in .env}:${GAME_SERVER_NETWORK_CONTAINER_PORT:?Set GAME_SERVER_NETWORK_CONTAINER_PORT in .env}/udp"
    healthcheck:
      test: ["CMD-SHELL", "curl --fail --silent http://localhost:$${GAME_SERVER_HTTP_PORT}/health/ready || exit 1"]
      interval: 5s
      timeout: 5s
      retries: 20
```

- [ ] **Step 3: Complete `game-server-2` in `docker-compose.yml`**

Replace the current `game-server-2` service body with:

```yaml
  game-server-2:
    profiles: ["game-servers"]
    build:
      context: .
      dockerfile: docker/game-server/Dockerfile
    environment:
      GAME_SERVER_ID: game-server-2
      GAME_SERVER_HTTP_PORT: ${GAME_SERVER_HTTP_CONTAINER_PORT:?Set GAME_SERVER_HTTP_CONTAINER_PORT in .env}
      GAME_SERVER_NETWORK_PORT: ${GAME_SERVER_NETWORK_CONTAINER_PORT:?Set GAME_SERVER_NETWORK_CONTAINER_PORT in .env}
      GAME_SERVER_PUBLIC_HOST: ${GAME_SERVER_PUBLIC_HOST:?Set GAME_SERVER_PUBLIC_HOST in .env}
      GAME_SERVER_PUBLIC_NETWORK_PORT: ${GAME_SERVER_2_NETWORK_HOST_PORT:?Set GAME_SERVER_2_NETWORK_HOST_PORT in .env}
    ports:
      - "127.0.0.1:${GAME_SERVER_2_HTTP_HOST_PORT:?Set GAME_SERVER_2_HTTP_HOST_PORT in .env}:${GAME_SERVER_HTTP_CONTAINER_PORT:?Set GAME_SERVER_HTTP_CONTAINER_PORT in .env}/tcp"
      - "127.0.0.1:${GAME_SERVER_2_NETWORK_HOST_PORT:?Set GAME_SERVER_2_NETWORK_HOST_PORT in .env}:${GAME_SERVER_NETWORK_CONTAINER_PORT:?Set GAME_SERVER_NETWORK_CONTAINER_PORT in .env}/udp"
    healthcheck:
      test: ["CMD-SHELL", "curl --fail --silent http://localhost:$${GAME_SERVER_HTTP_PORT}/health/ready || exit 1"]
      interval: 5s
      timeout: 5s
      retries: 20
```

- [ ] **Step 4: Validate Compose config**

Run:

```bash
docker compose --env-file .env.example --profile game-servers config
```

Expected: command exits 0 and shows both game-server services with HTTP and UDP ports.

- [ ] **Step 5: Commit Task 4**

Run:

```bash
git add docker-compose.yml .env.example
git commit -m "feat: add game server compose profile"
```

---

### Task 5: README and end-to-end smoke verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: Tasks 1-4
- Produces: documented Phase 02 commands and verification evidence

- [ ] **Step 1: Add README Phase 02 section**

Add a section that includes these commands:

```bash
Unity -batchmode -quit -projectPath . -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless
docker compose --env-file .env.example --profile game-servers build game-server-1 game-server-2
docker compose --env-file .env.example --profile game-servers up --detach --wait game-server-1 game-server-2
curl http://localhost:8091/health/ready
curl http://localhost:8092/health/ready
curl http://localhost:8091/status
curl http://localhost:8092/status
docker compose --env-file .env.example --profile game-servers restart game-server-1
docker compose --env-file .env.example --profile game-servers ps
docker compose --env-file .env.example --profile game-servers down
```

Also state that backend matchmaking/allocation is deferred to Phase 03 and that `Builds/` output is local and ignored by Git.

- [ ] **Step 2: Run server solution checks**

Run:

```bash
dotnet test server\LH.Main.Server.sln --configuration Release
dotnet build server\LH.Main.Server.sln --configuration Release
```

Expected: both commands exit 0. These checks prove Phase 02 did not regress backend/contracts.

- [ ] **Step 3: Run Unity Linux headless build**

Run:

```bash
Unity -batchmode -quit -projectPath . -executeMethod LH.Main.Unity.Editor.GameServerBuild.BuildLinuxHeadless
```

Expected: command exits 0 and writes `Builds/GameServer/LinuxHeadless/LH.Main.GameServer.x86_64`.

- [ ] **Step 4: Run Compose config check**

Run:

```bash
docker compose --env-file .env.example --profile game-servers config
```

Expected: command exits 0.

- [ ] **Step 5: Start two game-server containers**

Run:

```bash
docker compose --env-file .env.example --profile game-servers up --build --detach --wait game-server-1 game-server-2
```

Expected: both containers become healthy.

- [ ] **Step 6: Verify health and status**

Run:

```bash
curl http://localhost:8091/health/ready
curl http://localhost:8092/health/ready
curl http://localhost:8091/status
curl http://localhost:8092/status
```

Expected health responses: `{"status":"ok"}` for both ports.

Expected status fields: `serverId` is `game-server-1` and `game-server-2`, `state` is `idle`, `networkPort` is `7770`, `publicNetworkPort` is `7771` and `7772`.

- [ ] **Step 7: Verify restart isolation**

Run:

```bash
docker compose --env-file .env.example --profile game-servers restart game-server-1
docker compose --env-file .env.example --profile game-servers ps
curl http://localhost:8091/health/ready
curl http://localhost:8092/health/ready
```

Expected: `game-server-1` returns healthy after restart and `game-server-2` remains healthy.

- [ ] **Step 8: Stop Phase 02 containers**

Run:

```bash
docker compose --env-file .env.example --profile game-servers down
```

Expected: game-server containers stop. Do not stop the existing main `postgres` and `backend-api` containers unless the user explicitly asks.

- [ ] **Step 9: Run whitespace and status checks**

Run:

```bash
git diff --check
git status --short
```

Expected: no whitespace errors. Status shows only intended Phase 02 files plus pre-existing user Unity/IDE files.

- [ ] **Step 10: Commit Task 5**

Run:

```bash
git add README.md
git commit -m "docs: document game server phase"
```

---

## Final Review Checklist

- [ ] `Assets/Scripts/Server/*` compiles in Unity 6000.3.14f1.
- [ ] `ServerBootstrap.unity` is a technical scene only and does not modify content scenes.
- [ ] `Builds/GameServer/LinuxHeadless/` is ignored by Git.
- [ ] `docker compose --env-file .env.example --profile game-servers config` exits 0.
- [ ] Two game-server containers become healthy and return `idle` status.
- [ ] Restarting one game-server does not break the other.
- [ ] `dotnet test server\LH.Main.Server.sln --configuration Release` exits 0.
- [ ] `dotnet build server\LH.Main.Server.sln --configuration Release` exits 0.
- [ ] `git diff --check` exits 0 or reports only pre-existing line-ending warnings in user Unity/IDE files.
- [ ] No secrets, Unity `Library`, Unity `Temp`, build output, `.env`, or IDE artifacts are staged.
