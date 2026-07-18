# Univerus VSS — Vendor Self Service Portal

A real rebuild of the VSS design prototype (tenant: **City of Bozeman**) in Univerus
Unity/UDP conventions. Vendors self-manage their record; every edit becomes a
**change request** that City staff approve, which then syncs to the ERP vendor master.

This phase delivers the **vendor portal** end-to-end, a **real ASP.NET Core backend**,
and the **ERP integration stubbed behind an interface**. The admin portal UI is the
next phase (its approval endpoints already exist and are exercised by tests).

```
backend/    ASP.NET Core 10 Web API + EF Core (SQL Server) + ERP stub + xUnit tests
frontend/   React 18 + Vite + TypeScript (Unity/UDP conventions)
```

## Prerequisites

- .NET SDK 10
- SQL Server — defaults to a local **SQL Server Express** instance (`.\SQLEXPRESS`).
  Any SQL Server works; override via the `Vss` connection string.
- EF Core tools for migrations: `dotnet tool install --global dotnet-ef`
- Node.js 20+ (developed on 24) and npm

## Run the backend

```bash
cd backend
dotnet run --project Vss.Api --urls http://localhost:5047
```

- On startup it **applies EF Core migrations** to the `Vss` database on `.\SQLEXPRESS`
  (creating the database on first run) and seeds demo data.
- Swagger UI: http://localhost:5047/swagger
- Auth defaults to **Dev mode** (`Auth:Mode=Dev`): every request is the seeded user
  Dana Whitfield (vendor). Send `X-Dev-Role: admin` (and `X-Dev-*` headers) to act as
  City staff.

**Database / connection string.** Override the target via `ConnectionStrings:Vss`
(in `appsettings.json`, env var `ConnectionStrings__Vss`, or user-secrets) — e.g. use
`.\SQLEXPRESS01`, a named server, or SQL auth. Default (Windows auth):
`Server=.\SQLEXPRESS;Database=Vss;Trusted_Connection=True;TrustServerCertificate=True`

Migrations (run from `backend/`):

```bash
dotnet ef migrations add <Name> --project Vss.Infrastructure --startup-project Vss.Api
dotnet ef database update      --project Vss.Infrastructure --startup-project Vss.Api
```

Run the tests (they boot the API against SQLite in-memory, so no SQL Server is needed;
`DbInitializer` uses `EnsureCreated` for non–SQL Server providers and `Migrate` for SQL Server):

```bash
cd backend
dotnet test
```

## Run the frontend

```bash
cd frontend
cp .env.example .env   # already present with local defaults
npm install
npm run dev            # http://localhost:5173
```

Open http://localhost:5173 and walk the flow:
**Link record** (`V-10485` / PIN `4820`, or Tax ID `81-3920423` / ZIP `59715`) →
**Confirm** → **Console** → **Edit a section** (e.g. Banking) → **Submit for review**.

Other scripts: `npm run typecheck`, `npm run build`.

## Local-run seams (dev now, real network later)

App code is written to Unity/UDP conventions. Two things that can't resolve locally
are isolated behind env-switched seams — **app code does not change** between local and
network:

| Concern | Local (dev) | Univerus network |
|---|---|---|
| `@univerus/udp-react-enterprise-component-library` (private registry) | Aliased to `frontend/dev-stubs/…` in `vite.config.ts` + `tsconfig.json`. Re-implements only the used exports (`ConfigService`, `useApiQuery`/`useApiMutation`, `apiGet`/`apiMutate`, `RoleIdEnums`). | Remove the alias in both files, add the package to `dependencies`, `npm i`. |
| Auth | `REACT_APP_AUTH_MODE=dev` — a seeded fake user; requests carry `X-Dev-*` headers. | `REACT_APP_AUTH_MODE=entra` — real `MsalProvider`; requests carry a bearer token. Fill `REACT_APP_ENTRA_*` + `AzureAd` (backend). |
| Backend auth | `Auth:Mode=Dev` (`DevAuthHandler`). | `Auth:Mode=Entra` → `Microsoft.Identity.Web` JWT bearer (`AzureAd` config). |
| Database | Local SQL Express (`.\SQLEXPRESS`), Windows auth. | Point `ConnectionStrings:Vss` at the target SQL Server (same EF migrations apply). |
| UI primitives (`src/ui`) | App-local components on v4 tokens. | Swap for the library's design components. |

The data/config/auth layer already uses the **real** documented Unity APIs
(`ConfigService.*ApiUrl`, `useApiQuery`, `RoleIdEnums`, MSAL) — see
`src/udp-runtime-config.ts`, `src/api/vssClient.ts`, `src/auth/`.

## ERP integration

All ERP access goes through `IErpClient` (`backend/Vss.Infrastructure/Erp`). The current
`StubErpClient` serves seeded vendors, matches linking credentials, and logs the "push"
on approval. To go live, implement `UnityErpClient` against
`ConfigService.integrationV2ApiUrl` (`/vendors/{id}`, `/vendors/match`,
`/vendors/{id}/master`) and register it in `Program.cs` in place of the stub.

## Verified

- `dotnet test` — 4/4 passing (link → read → change → admin approve → ERP push); tests run
  on SQLite in-memory (real relational translation, no SQL Server dependency).
- Migrations apply cleanly to local **SQL Express** (`Vss` DB created + seeded on startup).
- `npm run build` / `npm run typecheck` — clean.
- Live click-through (dev auth + local backend on SQL Express): link → console → edit
  Banking → submit; admin approval pushes the change to the ERP stub and updates the record.

## Not in this phase

Full admin portal UI (link/change queues, diff review, ERP config screen, vendors grid),
the real `UnityErpClient`, document binary storage, and CI/UCP deployment manifests.
