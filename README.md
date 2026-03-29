# StorageVisualizer / Declutter

This repo now contains two versions of the app:

- A legacy Python prototype in the repo root
- A new `.NET 8` read-only local app in `src/StorageVisualizer.App`

The new `.NET` app is the recommended path forward. It keeps the current storage-visualization behavior, but moves the backend to a Windows-friendly stack and tightens the safety model without installing services, requesting elevation, or deleting anything.

## What Exists Today

### 1. Legacy Python prototype

These files are still present as a reference implementation:

- `ScannerEngine.py`
- `server.py`
- `index.html`

They provide:

- Recursive directory scanning
- Basic path safety checks
- A FastAPI endpoint
- A Plotly-based sunburst UI

### 2. New `.NET 8` local app

The active migration target lives here:

- `StorageVisualizer.sln`
- `src/StorageVisualizer.App`

This new app:

- Runs on `http://127.0.0.1:5080`
- Serves both the UI and the API from one local process
- Serves the app shell locally, while Plotly is still loaded from its CDN in this phase
- Scans folders in read-only mode
- Returns Plotly-ready sunburst data
- Loads installed-program locations from the Windows uninstall registry
- Flags folders that overlap installed-program paths as protected
- Performs bounded best-effort file-lock checks during scans
- Returns scan warnings and counters alongside the chart data
- Returns read-only cleanup recommendations for:
  - cache-like folders
  - installer or archive-heavy download-style folders
  - stale large folders
  - large flat folders
  - log-heavy folders
- Can query a separate privileged-agent boundary over a local named pipe
- Falls back to loopback-only TCP in this dev phase when the machine denies named-pipe connects
- Blocks known system paths such as `C:\Windows`
- Skips reparse points so it does not walk symlinks or junctions
- Skips noisy developer folders such as `.git`, `node_modules`, and `__pycache__`

## Current Safety Model

The new `.NET` app is intentionally conservative.

Implemented guardrails:

- Localhost-only hosting
- No delete operations
- No move operations
- No elevation
- No Windows service install
- No registry writes
- No shell integration
- Reparse-point root scanning is blocked
- `C:\Windows` scanning is blocked
- Permission and I/O errors are swallowed per-entry instead of crashing the whole scan
- Installed-program detection is read-only
- File-lock detection is best-effort and capped
- Cleanup recommendations are suggestions only and never queue actions
- Recommendation filtering and sorting happen in the UI only; they never affect the filesystem
- Privileged agent commands are allowlisted
- Dev-phase agent auth uses a shared token
- The app prefers a local named pipe and can fall back to loopback-only TCP on `127.0.0.1` in this phase
- The agent transport policy is configurable as `Auto`, `PipeOnly`, or `LoopbackOnly`
- Destructive commands are denied in this phase even if the agent is running

This means the current app is safe to treat as a visual inspection tool only.

## Architecture

### Current implementation

- `Program.cs`
  - Hosts the local ASP.NET Core app
  - Serves static files from `wwwroot`
  - Exposes:
    - `GET /health`
    - `GET /api/scan?targetPath=...`

- `Services/DirectoryScanner.cs`
  - Normalizes and validates the requested path
  - Enforces safety guardrails
  - Recursively enumerates files and directories
  - Totals folder sizes
  - Adds metadata for:
    - protected installed-program paths
    - suspected locked files
    - skipped reparse points
    - unreadable items

- `Services/SunburstFlattener.cs`
  - Converts the tree into:
    - `ids`
    - `labels`
    - `parents`
    - `values`
  - Adds:
    - `nodeDetails`
    - `summary`

- `Services/CleanupRecommendationEngine.cs`
  - Walks the scanned tree after facts are collected
  - Produces read-only review candidates for:
    - cache-like folders
    - installer or archive-heavy download-style folders
    - stale large folders
    - large flat folders with many direct files
    - log-heavy folders
  - Does not execute any filesystem operation

- `wwwroot/index.html`
  - Hosts the local UI
  - Calls the API with a relative path
  - Renders the Plotly sunburst chart
  - Shows warnings, summary counters, and per-node metadata in hover details
  - Shows whether the privileged agent is online and what it is allowed to do
  - Lets you filter and sort recommendation candidates by category, priority, size, and search text

- `StorageVisualizer.Protocol`
  - Shared named-pipe contract types used by both the app and the agent

- `StorageVisualizer.Agent`
  - Separate process boundary for future privileged work
  - Uses a local named pipe when available
  - Exposes a loopback-only TCP fallback in this dev phase
  - Honors a transport policy of `Auto`, `PipeOnly`, or `LoopbackOnly`
  - Uses shared-token authentication in this dev phase
  - Logs requests to a local audit log
  - Allows only `Ping` and `GetStatus`
  - Explicitly denies destructive commands

### Planned production direction

The production architecture from the migration plan is **not** fully implemented yet. The intended next phases are:

1. Keep the UI unprivileged and local
2. Keep scanning read-only by default
3. Add a separate privileged component only for destructive actions
4. Replace localhost HTTP for privileged actions with named-pipe IPC
5. Add Windows-native delete flows only after explicit confirmation and logging
6. Package the app cleanly for Windows deployment

Those steps are intentionally deferred because they carry more operational and safety risk than the current scanner.

## Run The New `.NET` App

From the repo root:

```powershell
dotnet run --project .\src\StorageVisualizer.App\StorageVisualizer.App.csproj
```

Then open:

```text
http://127.0.0.1:5080
```

## Run The Agent

This is optional in phase 2. The app works without it, but the UI will show the agent as offline.

From the repo root:

```powershell
dotnet run --project .\src\StorageVisualizer.Agent\StorageVisualizer.Agent.csproj
```

The agent:

- listens on the `storage-visualizer-agent-sawaa-diskmap` named pipe
- listens on `127.0.0.1:5091` as a dev fallback when named-pipe connects are denied on the machine
- respects the transport policy configured in:
  - `src/StorageVisualizer.App/appsettings.json`
  - `src/StorageVisualizer.Agent/agentsettings.json`
- authenticates requests with the shared token configured in:
  - `src/StorageVisualizer.App/appsettings.json`
  - `src/StorageVisualizer.Agent/agentsettings.json`
- writes audit entries to `logs\agent-audit.log`
- still refuses destructive commands

## Build

```powershell
dotnet build .\src\StorageVisualizer.Protocol\StorageVisualizer.Protocol.csproj
dotnet build .\src\StorageVisualizer.Agent\StorageVisualizer.Agent.csproj
dotnet build .\src\StorageVisualizer.App\StorageVisualizer.App.csproj
```

## Verified Behavior

The new `.NET` app has been locally verified for:

- `GET /health` returns `200`
- `GET /api/scan?targetPath=.` returns `200`
- `GET /api/scan?targetPath=C:\Windows` returns `403`
- `GET /api/scan` now returns `summary`, `nodeDetails`, and `recommendations`
- `GET /api/scan` emits recommendation candidates for a controlled cache-like scan fixture
- `GET /api/scan` emits the refined categories `Cache-like data`, `Installer or archive stash`, `Stale large folder`, and `Log-heavy folder` from a controlled fixture
- `GET /api/agent/status` reports the agent as offline when it is not running
- `GET /api/agent/status` reports the configured transport policy and enabled transports
- `GET /api/agent/status` reports the agent as online when the agent is running, using loopback fallback on this machine

## Repo Layout

```text
.
|-- StorageVisualizer.sln
|-- ScannerEngine.py
|-- server.py
|-- index.html
|-- src
|   |-- StorageVisualizer.App
|   |   |-- Program.cs
|   |   |-- Configuration
|   |   |-- Models
|   |   |-- Services
|   |   |-- Properties
|   |   `-- wwwroot
|   |-- StorageVisualizer.Protocol
|   |   `-- *.cs
|   `-- StorageVisualizer.Agent
|       |-- Program.cs
|       |-- Services
|       `-- agentsettings.json
`-- README.md
```

## What Is Not Implemented Yet

These production features are still pending:

- Hardened OS-level pipe ACL enforcement
- Privileged Windows service
- `IFileOperation` shell-aware delete
- Recycle Bin integration
- VSS integration
- MSI or MSIX packaging
- Signing and update flow

That is deliberate. The current goal is to migrate the scanner safely before any destructive capability exists.
