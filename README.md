# NatureOS Core API

> **.NET 8 backend** for the NatureOS Earth-systems platform  
> **Deployed to**: Azure App Service `natureos-api-prod001`  
> **Repo**: `MycosoftLabs/NatureOS`

## What this repo contains

| Directory | Content |
|-----------|---------|
| `src/core-api/` | ASP.NET Core 8 Web API — controllers, services, SignalR hub |
| `src/mindex/` | MINDEX model library (`MycorrhizaeEvent`, `MycoBrainModels`, taxonomy types) |
| `src/mycorrhizae/` | MDPv1 binary protocol codec |
| `src/ingestion/` | Azure Functions v4 workers (event, MycoBrain, MAS ingestion) |
| `tests/NatureOS.Tests/` | xUnit unit tests (run in CI) |
| `src/dashboard/` | Next.js dashboard (has its own `package.json`) |
| `matlab/` / `simulink/` | MATLAB analysis scripts and Simulink models |
| `sensor-bme680/` / `sensor-i2c-scan/` / `uplink-t-sim7000g/` | PlatformIO firmware for field sensors |
| `infrastructure/` | Azure Bicep IaC (`main.bicep`) |
| `docker/` / `Dockerfile` / `docker-compose.yml` | Local dev stack (API, CosmosDB emulator, Redis, Grafana) |

## Solution structure (`NatureOS.sln`)

```
NatureOS.CoreApi        → src/core-api/NatureOS.CoreApi.csproj
NatureOS.MINDEX         → src/mindex/NatureOS.MINDEX.csproj
NatureOS.Mycorrhizae    → src/mycorrhizae/NatureOS.Mycorrhizae.csproj
NatureOS.Ingestion      → src/ingestion/NatureOS.Ingestion.csproj
NatureOS.Tests          → tests/NatureOS.Tests/NatureOS.Tests.csproj
```

## Quick start

```bash
# Restore and build
dotnet restore NatureOS.sln
dotnet build NatureOS.sln --configuration Release

# Run tests
dotnet test tests/NatureOS.Tests/

# Run locally (requires Azure config — see .env.example)
dotnet run --project src/core-api/
```

Or with Docker:

```bash
docker compose up -d natureos-api cosmos-emulator redis
```

The API listens on port **8080** and exposes `/health`, `/version`, and `/api/status`.

## Key API routes

| Route | Purpose |
|-------|---------|
| `GET /health` | ASP.NET health-check (Cosmos, SignalR) |
| `GET /api/status` | Lightweight version/feature probe |
| `GET /api/mycosoft/status` | Full system status (devices, events, MAS health) |
| `POST /api/mycosoft/myca/query` | MYCA AI query (live when `MYCA_API_URL` is set, synthetic fallback otherwise) |
| `GET /api/mycosoft/website/dashboard` | Dashboard payload for website integration |
| `GET /api/mycosoft/events/stream` | SSE real-time event stream |
| `POST /api/mycobrain/telemetry` | MycoBrain device telemetry ingestion |
| `PUT /devices/{id}/config` | MAS-compatible device config update (upserts if not registered) |
| `WS /natureos-hub` | SignalR hub for real-time push |

## Configuration

Copy `.env.example` to `.env` (never commit `.env`).

Required secrets for production are documented in `docs/key-vault-configuration.md`
and `docs/github-secrets-setup.md`. Key variables:

| Variable | Purpose |
|----------|---------|
| `MINDEX_API_URL` | MINDEX service base URL |
| `MINDEX_API_KEY` | MINDEX API key |
| `MYCA_API_URL` | Live MYCA backend (omit for synthetic fallback) |
| `CORS_ORIGINS` | Comma-separated allowed origins |

## CI / CD

`.github/workflows/deploy-production.yml` runs on push/PR to `main`:

1. `dotnet restore` → full solution
2. `dotnet build` → all projects
3. `dotnet test` → `tests/NatureOS.Tests/`
4. `dotnet publish` → Core API artifact
5. Deploy to Azure App Service `natureos-api-prod001` (main branch only)

## Relationship to other repos

- **`MycosoftLabs/website`** — the Mycosoft Website (Next.js). NatureOS is consumed via REST/SignalR; the website owns the frontend, system architecture docs, and Fusarium digital-twin UI.
- **MINDEX / MAS** — NatureOS projects events into MINDEX and MAS containers in Azure Cosmos DB. MAS orchestration lives outside this repo.
- **Fusarium** — the two `/api/mycosoft/fusarium/*` endpoints in this repo are thin proxies over the standard dashboard data. All Fusarium-specific logic (digital twin, environmental modelling) lives in the website + MINDEX + MAS ecosystem, not here.

## Documentation

- [Mycosoft Integration](./docs/mycosoft-integration.md)
- [MycoBrain Integration](./docs/mycobrain-integration.md)
- [Frontend Integration Guide](./docs/frontend-integration-guide.md)
- [Key Vault Configuration](./docs/key-vault-configuration.md)
- [GitHub Secrets Setup](./docs/github-secrets-setup.md)

## License

Copyright © 2026 Mycosoft. All rights reserved. See [LICENSE](./LICENSE).
