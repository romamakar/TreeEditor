![.net build and test](https://github.com/romamakar/TreeEditor/actions/workflows/dotnet.yml/badge.svg?branch=master)
# Tree Editor

Backend: .NET 10 Minimal API (TreeEditor.Api)
Frontend: ASP.NET Core MVC (TreeEditor.Web) serving a small JavaScript UI
Database: SQLite (tree.db)
Cache: in-memory per-user caches keyed by client GUID

Prerequisites:

- .NET 10 SDK installed

Run:
Option A — single command (recommended):

Windows (PowerShell):

1. From repository root run the helper script which opens two PowerShell windows and starts both projects:

   `.\run-dev.ps1`

   - This opens separate PowerShell windows for TreeEditor.Api and TreeEditor.Web and runs `dotnet run --no-launch-profile` in each on ports 5001 and 5003 respectively.

Unix / macOS:

1. From repository root run the helper script which starts both projects in the background and writes logs to `logs/`:

   `./run-dev.sh`

   - Logs are written to `logs/api.log` and `logs/web.log; use `tail -f` to follow them.

Option B — manual (any platform):

1. Open one terminal for the API and run:

   `cd TreeEditor.Api`
   `dotnet run --no-launch-profile --urls 'https://localhost:5001'`

   (API defaults to https://localhost:5001; note the port shown in console)

2. Open a second terminal for the Web app and run:

   `cd TreeEditor.Web`
   `dotnet run --no-launch-profile --urls 'https://localhost:5003'`

3. Open the Web UI (the console will show the URL, e.g., https://localhost:5003) and navigate to Home → Tree Editor or /Home/Editor.

If the API runs on a different origin than the Web app, configure the API base used by the frontend. By default the Web view uses https://localhost:5001. You can override it in TreeEditor.Web/Views/Home/Editor.cshtml (window.apiBase) or set an "ApiBase" configuration value in the Web app's settings.

Reset: use the Reset button in the UI to restore sample data.

Database schema:
- Elements(Id INTEGER PK AUTOINCREMENT, ParentId INTEGER NULL, Value TEXT NOT NULL, IsDeleted BOOLEAN NOT NULL DEFAULT 0)

Notes:
- Per-user in-memory cache: the frontend generates a client GUID (stored in localStorage key "tree-editor-client-id") and sends it as `X-Client-Id` on every API request. The API keeps separate in-memory caches per client GUID.
- DBTreeView fetches children lazily from GET `/api/children` (omit parentId for roots).
- CachedTreeView shows cached elements and supports `edit/add/delete`; changes remain in cache until Apply (POST `/api/cache/apply`) persists them to the DB.
- Deleting an element marks it and its subtree IsDeleted in the DB using a recursive CTE.
- New elements in cache use temporary negative ids until Apply assigns real ids.

This repository contains minimal code to demonstrate the required behavior. Build and run using the commands above.

Tests
- Unit tests for the cache are in tests/TreeEditor.Api.Tests and use an in-memory SQLite connection to exercise Apply logic (recursive CTEs). Run with:
  `dotnet test tests/TreeEditor.Api.Tests`
