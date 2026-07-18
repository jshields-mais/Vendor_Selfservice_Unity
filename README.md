# Univerus VSS — Vendor Self Service Portal

A real rebuild of the VSS design prototype (tenant: **City of Bozeman**) in Univerus
Unity/UDP conventions. Vendors self-manage their record; every edit becomes a
**change request** that City staff approve, which then syncs to the ERP vendor master.

Delivers the **vendor portal** and the **City-staff admin portal** end-to-end, a
**real ASP.NET Core backend**, and the **ERP integration stubbed behind an interface**.
In dev, switch between the two portals with the sidebar's "Demo: view as City staff"
toggle.

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

All ERP access goes through `IErpClient` (`backend/Vss.Infrastructure/Erp`). The active
connector is chosen by **`Erp:Provider`** and registered by `AddErpClient(...)`:

| `Erp:Provider` | Connector | Transport / auth |
|---|---|---|
| `Stub` (default) | `StubErpClient` | in-memory seeded suppliers (dev) |
| `BusinessCentral` | `BusinessCentralErpClient` | Dynamics 365 BC OData v2.0 REST; OAuth2 client-credentials (Entra ID); GET/PATCH + ETag |
| `SapByDesign` | `SapByDesignErpClient` | SAP ByDesign SOAP — `QuerySupplierIn` (read/match) + `ManageSupplierIn` (write); HTTP Basic |

Each maps to `IErpClient.GetVendorAsync` / `MatchVendorAsync` / `UpdateVendorMasterAsync`
(approval calls `UpdateVendorMasterAsync`, which pushes the change to the ERP master).

**Going live** (config in `appsettings.json` / env; **secrets only via user-secrets/env/k8s Secret**):

```bash
cd backend/Vss.Api
# Business Central
dotnet user-secrets set "Erp:Provider" "BusinessCentral"
dotnet user-secrets set "Erp:BusinessCentral:BaseUrl"   "https://api.businesscentral.dynamics.com/v2.0/<tenant>/<env>/api/v2.0"
dotnet user-secrets set "Erp:BusinessCentral:CompanyId" "<company-guid>"
dotnet user-secrets set "Erp:BusinessCentral:TenantId"  "<entra-tenant-id>"
dotnet user-secrets set "Erp:BusinessCentral:ClientId"  "<app-client-id>"
dotnet user-secrets set "Erp:BusinessCentral:ClientSecret" "<secret>"        # secret
dotnet user-secrets set "Erp:BusinessCentral:SampleVendorNumber" "<a-known-vendor>"

# SAP Business ByDesign
dotnet user-secrets set "Erp:Provider" "SapByDesign"
dotnet user-secrets set "Erp:SapByDesign:BaseUrl"  "https://myNNNNNN.sapbydesign.com"
dotnet user-secrets set "Erp:SapByDesign:Username" "<comm-arrangement-user>"
dotnet user-secrets set "Erp:SapByDesign:Password" "<secret>"                # secret
dotnet user-secrets set "Erp:SapByDesign:SampleSupplierId" "<a-known-supplier>"
```
(env-var form for containers: `Erp__BusinessCentral__ClientSecret`, `Erp__SapByDesign__Password`, …)

Then **verify connectivity** from the admin **ERP integration** screen → *Test connection*
(or `POST /api/v1/admin/erp/test`), which pings the configured ERP and reports
`{provider, ok, latencyMs, message}`.

**Notes for live wiring:**
- **SAP ByDesign** message shapes are confirmed against a live tenant (City of Jacksonville)
  and the WSDLs in `Erp/SapByDesign/wsdl/`: request **body** elements are in
  `http://sap.com/xi/SAPGlobal20/Global`, while the **SOAPAction** uses the `A1S/Global`
  service namespace; `SelectionByInternalID` filters via `LowerBoundaryIdentifier`
  (`IntervalBoundaryTypeCode` 1 = equal); Manage uses `MaintainBundle_V1`. Matching is by
  supplier number (InternalID).
- **SAP write coverage (verified live, change → approve → SAP):**
  - ✅ **Name** (`FirstLineName`, direct on the supplier bundle).
  - ✅ **Address + primary email/phone** — via `AddressInformation`, which needs LCTI
    (`addressInformationListCompleteTransmissionIndicator`) + the existing address UUID, so
    the connector reads the supplier first to update the address in place.
  - ⏳ **Banking** (`BankDetails`) — needs a bank in the ByDesign **bank directory**
    (`BankRoutingID`/`BankInternalID`) plus its own UUID/LCTI; not yet mapped.
  - ⏳ **Contacts** (names) — separate `ContactPerson` business-partner entities; not yet mapped.
  - ⚠️ **Tax (TIN)** — `ManageSupplierIn` exposes no direct TIN field (only
    `DeviantTaxClassification`); likely not updatable via this service.
- **Dev credential convenience**: `Erp:SapByDesign:CredentialsFile` — point it at a text file
  whose last non-empty line is the technical-user password; the app reads it at startup
  (like a mounted secret). Prefer user-secrets / env / a K8s Secret for real deployments.
- **Business Central** banking fields (routing/account) aren't on the standard `vendor`
  entity — they live under `vendorBankAccounts` (a second call, currently skipped/TODO).
- The invitation **PIN** has no ERP equivalent; ERP matching is by vendor number or Tax ID +
  ZIP, and the PIN is verified app-side.

## Verified

- `dotnet test` — 4/4 passing (link → read → change → admin approve → ERP push); tests run
  on SQLite in-memory (real relational translation, no SQL Server dependency).
- Migrations apply cleanly to local **SQL Express** (`Vss` DB created + seeded on startup).
- `npm run build` / `npm run typecheck` — clean.
- Live click-through (dev auth + local backend on SQL Express): link → console → edit
  Banking → submit; admin approval pushes the change to the ERP stub and updates the record.

## Not in this phase

Document binary storage; persisting ERP connection settings from the admin ERP screen
(its config fields are presentational — only *Test connection* is live); and the
connector TODOs above (ByDesign WSDL specifics, BC `vendorBankAccounts`). Dev-only
`POST /api/v1/dev/reset` wipes and reseeds the database (404s unless `Auth:Mode=Dev`).

## CI / Deployment

- **CI** — [`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on push and PRs to
  `main`: a **backend** job (`dotnet test`, SQLite in-memory — no SQL Server) and a
  **frontend** job (`npm ci` → `npm run typecheck` → `npm run build`).
- **Deployment** — [`deploy/`](deploy/) holds Dockerfiles and Kubernetes manifests for
  UCP. These are **templates with placeholders** (registry/image tags, `vss-secrets`,
  host, Entra config, ingress class) — see [`deploy/README.md`](deploy/README.md) for what
  to fill in. Not production-ready; a real `/health` endpoint should be added to the API.
