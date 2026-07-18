# Deployment

## Quickest way to share it for testing — Docker Compose

`docker-compose.yml` (repo root) runs the whole app — **SQL Server + API + frontend** —
behind one URL. Anywhere with Docker works: your PC, or a small cloud VM you share.

```bash
docker compose up -d --build     # first run builds the images (a few minutes)
# open http://localhost:8080
docker compose logs -f backend   # watch startup / migrations
docker compose down              # stop (keeps the database volume)
docker compose down -v           # stop and wipe the database
```

To share with others, run it on a cloud VM (any provider) with port **8080** open and give
testers `http://<vm-public-ip>:8080`. Change the DB password with `SA_PASSWORD=... docker
compose up -d --build`, or the public port with `PORT=80`.

**Two things to know before inviting testers:**

- **ERP is the safe in-memory stub by default** (`Erp__Provider=Stub`) — testers can run the
  full flow (link, edit, upload, review, approve) with **zero risk to the live SAP sandbox**.
  To point at live SAP instead, set `ERP_PROVIDER=SapByDesign` plus `SAP_BASE_URL`,
  `SAP_USERNAME`, and `SAP_PASSWORD` (env vars — never commit them), e.g. in a local `.env`.
- **There is no real login.** Auth runs in dev mode and the UI has a "view as City staff"
  toggle, so anyone can act as admin. Fine for a **trusted** group; don't expose it on the
  public internet as-is. Real logins come with the Entra path below.

---

## Production scaffolding (UCP / Kubernetes)

> **Scaffolding, not production-ready.** Everything here is a **template** with
> placeholders. Review and fill in the UCP-specific values before deploying.

## What's here

| File | Purpose |
|---|---|
| `backend.Dockerfile` | Multi-stage build of the `Vss.Api` service (SDK 10 → aspnet 10 runtime), listens on `8080`. Build with the **repo root** as context. |
| `frontend.Dockerfile` | Builds the Vite/React app (`npm ci && npm run build`) and serves `dist/` via nginx. Build with the **repo root** as context. |
| `nginx.conf` | Minimal SPA (history-API) fallback config for the frontend image. |
| `k8s/backend-deployment.yaml` / `backend-service.yaml` | Backend Deployment + ClusterIP Service. |
| `k8s/frontend-deployment.yaml` / `frontend-service.yaml` | Frontend Deployment + ClusterIP Service. |
| `k8s/ingress.yaml` | Ingress routing host → frontend and `/api` → backend. |

## Building the images

Both Dockerfiles expect the **repository root** as the build context:

```bash
docker build -f deploy/backend.Dockerfile  -t REGISTRY/vss-backend:TAG  .
docker build -f deploy/frontend.Dockerfile -t REGISTRY/vss-frontend:TAG .
```

## What you must fill in

- **Registry / image tags** — replace `REGISTRY/vss-backend:TAG` and
  `REGISTRY/vss-frontend:TAG` everywhere with your registry and an immutable tag.
- **`vss-secrets` Secret** — create it with key `db-connection` (the SQL Server
  connection string for `ConnectionStrings__Vss`). Add Entra/`AzureAd` values here too.
- **ERP config + secrets** — set `Erp__Provider` (`BusinessCentral` | `SapByDesign`) and
  its non-secret settings via env; put secrets in `vss-secrets`:
  `Erp__BusinessCentral__ClientSecret` or `Erp__SapByDesign__Password`. See the root
  README "ERP integration" for the full key list.
- **Entra config** — the backend runs with `Auth__Mode=Entra`; supply the
  `Microsoft.Identity.Web` / `AzureAd__*` settings. The frontend bakes
  `REACT_APP_*` (auth mode, Entra, Unity, API domain) in at **image build time**
  — pass them as build args (see `frontend.Dockerfile`).
- **Host** — set the real public host in `k8s/ingress.yaml` (and add a `tls:` block).
- **Ingress class / annotations** — set the real UCP ingress class and any
  required UCP annotations (TLS/cert-manager, rewrites, auth).
- **`Frontend__Origin`** — set to the real frontend origin (backend CORS).

## Known gap: health checks

The Deployment probes currently target `/` because the only richer endpoint,
Swagger, is **dev-only**. **Add a real `/health` endpoint to `Vss.Api`** and
repoint the readiness/liveness probes at it before production.
