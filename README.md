# Tree Editor

Backend: .NET 10 Minimal API (TreeEditor.Api)
Frontend: Blazor Server (TreeEditor.Web)
Database: SQLite (tree.db)
Cache: in-memory singleton

Run:
1. Open two terminals.
2. Start API:
   cd TreeEditor.Api
   dotnet run --no-launch-profile
   (API defaults to https://localhost:5001; note the port shown in console)
3. Start UI:
   cd ../TreeEditor.Web
   dotnet run --no-launch-profile
4. Open the Blazor UI (the console will show the URL, e.g., https://localhost:5003).

If the API runs on a different port than https://localhost:5001, set environment variable ApiBase when starting the Web app, e.g.:
   dotnet run --no-launch-profile --urls "https://localhost:5003" -- ApiBase="https://localhost:5001"

Reset: use the Reset button in the UI to restore sample data.

Database schema:
- Elements(Id INTEGER PK AUTOINCREMENT, ParentId INTEGER NULL, Value TEXT NOT NULL, IsDeleted BOOLEAN NOT NULL DEFAULT 0)

Notes:
- The cache is server-side (singleton) and holds loaded elements and pending adds/edits/deletes until Apply is clicked.
- Deleting an element marks it (and its subtree in the DB) as deleted on Apply using a recursive CTE.
- New elements created in the cache get temporary negative ids until Apply persists them and assigns real ids.

This repository contains minimal code to demonstrate the required behavior. Build and run using the commands above.
