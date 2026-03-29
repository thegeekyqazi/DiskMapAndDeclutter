# Storage Visualizer / Declutter

Storage Visualizer is a local-first Windows storage review app. It helps you understand what is taking space, flag folders that need extra caution, find likely duplicate files, find older large files, inspect file contents safely, and keep your own review notes while you decide what to clean up manually.

The current app is designed to be useful before it becomes destructive. It does **not** delete, move, rename, or modify files in the folders you scan. The only write behavior in the current app is saving your local review state, such as favorites, hidden items, reviewed items, decisions, and notes, into the app's own workspace folder.

## The Idea

Most storage tools jump too quickly from "scan" to "delete." This project is trying to solve the step in between:

- show where the space is
- highlight things that look worth reviewing
- help you keep track of what you already checked
- surface duplicate and stale-file candidates 
- let you inspect file contents before acting
- keep strong guardrails so a mistake in the app does not become a mistake on your PC

The app is meant to be a safe review workspace first, and only later a cleanup tool if the safety model is strong enough.

## What The App Can Do Right Now

### 1. Folder scan and storage map

The main scan:

- walks a folder tree
- totals folder sizes
- builds a Plotly sunburst chart
- shows only child folders above a minimum size threshold
- adds warnings and counters so the chart is not just visual noise

What you see in the UI:

- a large sunburst map of the scanned folder
- summary counters
- scan warnings
- scan facts
- node-level hover details

This is powered by:

- [Program.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Program.cs)
- [DirectoryScanner.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/DirectoryScanner.cs)
- [SunburstFlattener.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/SunburstFlattener.cs)

### 2. Safety-aware scan metadata

The scanner does more than count bytes. It also tracks signals that make a folder riskier or more interesting to review:

- protected installed-program overlap
- unreadable items
- skipped reparse points
- skipped noisy folders such as `.git`, `node_modules`, and `__pycache__`
- best-effort file lock hints
- counts of direct files, log files, archive files, installer files, and large files
- latest content write time

This is why the app can say "this folder is large" and also "this folder overlaps an installed app path" or "some files here may be in use."

### 3. Review recommendation cards

After a scan, the app creates read-only review cards for folders that look worth checking manually. These are suggestions, not actions.

Current recommendation types include:

- cache-like folders
- installer or archive stashes
- stale large folders
- large flat folders
- log-heavy folders

Each card gives:

- a title
- a category
- a reason
- an estimated size
- supporting signals

This logic lives in [CleanupRecommendationEngine.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/CleanupRecommendationEngine.cs).

### 4. Reviewed / hidden / favorite workflow

Each review card can now be tracked locally. You can:

- mark a card as favorite
- mark a card as reviewed
- hide a card
- attach a manual decision such as keep, later, archive, or delete later
- add a private note

This is useful because storage cleanup is usually multi-pass work, not a one-click action.

Important detail:

- this state is saved only to the app workspace
- the scanned folders are not modified

This is implemented in:

- [ReviewEntry.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Models/ReviewEntry.cs)
- [ReviewWorkspaceStore.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/ReviewWorkspaceStore.cs)

### 5. Duplicate file finder

The duplicate analysis is read-only. It does not remove anything. It works like this:

- enumerate files under the current scan root
- keep only files above the duplicate-size threshold
- group by exact size
- hash a partial chunk first
- hash the full file to confirm likely duplicates
- show the top duplicate groups

For each duplicate group, the app shows:

- files in the group
- size per file
- estimated reclaimable duplicate bytes
- protection flags
- possible lock flags

This feature is implemented in [DuplicateFileAnalysisService.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/DuplicateFileAnalysisService.cs).

### 6. Stale file finder

The stale-file view is also read-only. It looks for files that are:

- above a minimum size
- old enough based on last write / last access activity
- inside the current scan root

This helps you surface files that might be archive candidates or just forgotten clutter.

What it returns:

- file path
- file size
- extension
- last activity time
- protection flags
- possible lock flags

This feature is implemented in [StaleFileAnalysisService.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/StaleFileAnalysisService.cs).

### 7. File inspector

The file inspector lets you look deeper at a file before you decide it is useless.

Current behaviors:

- text-like files show the first readable lines
- `.zip` files show archive entry names
- binary files show a short hex preview
- metadata is returned alongside the preview

This makes the app more than just a size viewer. You can actually ask, "What is this file?" without leaving the tool.

This feature is implemented in [FileInspectionService.cs](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/Services/FileInspectionService.cs).

### 8. Optional helper boundary

There is a separate helper process for future privileged work. Right now it exists mainly as a boundary and status surface, not as a destructive engine.

Current state:

- the main app works without it
- the UI shows whether it is online
- allowed commands are still tightly restricted
- destructive commands are denied in the current phase

This lives in:

- [StorageVisualizer.Agent](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.Agent)
- [StorageVisualizer.Protocol](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.Protocol)

## Current Safety Model

This is the part that matters most.

The app currently includes these guardrails:

- local hosting only on `127.0.0.1`
- no delete operations
- no move operations
- no rename operations
- no elevation prompts
- no Windows service install in the main app flow
- no registry writes
- blocked scanning for `C:\Windows`
- blocked reparse-point root scanning
- skipped reparse points during traversal
- bounded best-effort lock checking
- installed-program path protection from uninstall registry data
- review-state writes limited to the app's own data folder
- file inspection restricted to paths inside the current scan root
- duplicate and stale-file analyses restricted to the current scan root

What this means in practice:

- the app is useful
- the app is not harmless because any software can still have bugs
- but the current design is intentionally biased toward review and analysis, not cleanup execution

## What The UI Is Trying To Be

The front end is not just a chart page anymore. It is now a storage review workspace with five main areas:

1. Scan panel  
Enter a root path and run a safe scan.

2. Storage map  
See the scanned folder tree as a sunburst chart.

3. Review cards  
Track the folders you care about with favorite / reviewed / hidden state and notes.

4. File analysis workspace  
Run duplicate and stale-file analysis.

5. File inspector  
Preview file contents safely before deciding what to do manually.

The current front end is in:

- [index.html](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/wwwroot/index.html)
- [app.css](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/wwwroot/app.css)
- [app.js](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/src/StorageVisualizer.App/wwwroot/app.js)

## How To Start The App

### Prerequisites

You need:

- .NET 8 SDK
- Windows

### Start the main app

From the repo root:

```powershell
dotnet run --project .\src\StorageVisualizer.App\StorageVisualizer.App.csproj
```

Then open:

```text
http://127.0.0.1:5080/
```

Health check:

```text
http://127.0.0.1:5080/health
```

### Optional: start the helper process

The main app does not require the helper, but you can run it if you want the helper status panel to come online.

From the repo root:

```powershell
dotnet run --project .\src\StorageVisualizer.Agent\StorageVisualizer.Agent.csproj
```

## How To Use The App

### Basic workflow

1. Start the app.
2. Paste a folder path you actually own.
3. Run a scan.
4. Review the warnings first.
5. Use the storage map to spot large areas.
6. Use the review cards to track what deserves manual follow-up.
7. Run duplicate analysis if you suspect repeated files.
8. Run stale-file analysis if you want to find old large files.
9. Use the file inspector before deciding a file is junk.
10. Save your review state so you can come back later.

### Good folders to start with

- Downloads
- Desktop
- Documents
- project workspaces
- personal media folders you control

### Folders you should not expect the app to let you scan

- `C:\Windows`
- reparse-point roots

## API Surface

The app serves both the UI and a small local API.

### `GET /health`

Returns a simple app health response.

### `GET /api/scan?targetPath=...`

Runs the main safe scan and returns:

- sunburst arrays
- node details
- summary data
- review recommendations

### `GET /api/review-state?rootPath=...`

Loads saved local review state for the current scan root.

### `POST /api/review-state`

Saves local review state for the current scan root.

### `GET /api/analysis/duplicates?rootPath=...`

Runs duplicate analysis under the current root.

### `GET /api/analysis/stale-files?rootPath=...`

Runs stale-file analysis under the current root.

### `GET /api/analysis/file-inspect?rootPath=...&targetPath=...`

Inspects a file inside the current root and returns a safe preview.

### `GET /api/agent/status`

Returns helper status and transport information.

## Build Commands

```powershell
dotnet build .\src\StorageVisualizer.Protocol\StorageVisualizer.Protocol.csproj
dotnet build .\src\StorageVisualizer.Agent\StorageVisualizer.Agent.csproj
dotnet build .\src\StorageVisualizer.App\StorageVisualizer.App.csproj
```

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
|   `-- StorageVisualizer.Agent
`-- README.md
```

## Legacy Python Prototype

The repo still contains the original Python prototype in the root:

- [ScannerEngine.py](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/ScannerEngine.py)
- [server.py](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/server.py)
- [index.html](c:/Users/Sawaa/Documents/DiskMapAndDeclutter/index.html)

That version is useful as historical reference, but the `.NET` app is now the main implementation.

## What Is Not Implemented Yet

These are still intentionally missing:

- real delete execution
- move execution
- rename execution
- Recycle Bin integration
- shell-aware `IFileOperation` delete flows
- hardened OS-level pipe ACL enforcement
- a production Windows service model
- installer packaging and signing
- update flow

Those are exactly the parts that can go wrong in expensive ways, so they are being deferred until the analysis and safety layers are solid.

## Current Limitations

- Plotly is still loaded from a CDN, so the app shell is local but not fully offline
- lock detection is best-effort, not authoritative
- installed-program protection depends on usable uninstall registry install paths
- last-access timestamps on Windows can be approximate
- duplicate analysis is capped on purpose for responsiveness
- file inspection is intentionally shallow and conservative

## Practical Summary

Right now this project is best understood as:

- a safe local storage map
- a duplicate and stale-file review tool
- a lightweight file inspection tool
- a personal review workspace for cleanup planning

It is **not** yet a real cleaner, and that is deliberate.
