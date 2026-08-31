# NatureOS Issues #5–#9 — Resolution Plan (2026-08-31)

## Repository context

NatureOS is a .NET 8 (C#) Azure-deployed API backend.
The repo contains the **core-api**, **MINDEX** models, **Mycorrhizae** protocol library,
an **Ingestion** Azure Functions project, sensor firmware, MATLAB scripts,
and infrastructure-as-code (Bicep).

The Mycosoft Website is in a **separate repo** (`MycosoftLabs/website`).
NatureOS does not embed or serve the website — it exposes REST + SignalR APIs
that the website consumes.

---

## Issue-by-issue status

### #9 — Run NatureOS unit tests in CI ✅ FIXED

**Problem**: No test project existed; CI ran `dotnet test` on the main project (no-op).

**What was done**:
- Created `tests/NatureOS.Tests/` with 26 xUnit tests covering:
  - TopSpecies derivation logic (8 tests)
  - MycaResponse synthetic flag (3 tests)
  - MAS health result model (3 tests)
  - Device upsert semantics (5 tests)
  - MycorrhizaeEvent model baseline (7 tests)
- Added project to `NatureOS.sln`
- Updated `.github/workflows/deploy-production.yml` to restore the full solution
  and run `dotnet test tests/NatureOS.Tests/NatureOS.Tests.csproj`
- All 26 tests pass locally on .NET 8.0.

**Remaining work**: None for this repo. CI will pick up the new workflow on merge.

---

### #8 — Replace write-based MAS health probe with read-only check ✅ FIXED

**Problem**: The `/api/mycosoft/status` endpoint reported `MAS = "Unknown"`.
The issue described a write-based probe that created `health_check` documents
in `mas_context`. That code was already removed (or never landed), but the
status endpoint still returned a hardcoded "Unknown" string.

**What was done**:
- Added `IMasIngestionService.CheckHealthAsync()` — a read-only
  `SELECT VALUE COUNT(1) FROM c` query against `mas_context`.
- Added `MasHealthResult` record with `Healthy`, `Status`, `Detail`.
- `/api/mycosoft/status` now calls `CheckHealthAsync()`, reports
  `MAS = "Healthy"/"Unhealthy"` with detail, and degrades `Overall`
  to `"Degraded"` when MAS is unreachable.
- No documents are written during the health check.

**Remaining work**: None. The fix is purely in this repo's API layer.

---

### #7 — Derive TopSpecies from taxonomy fields ✅ FIXED

**Problem**: `GetSystemContext()` returned `TopSpecies = Array.Empty<string>()`.

**What was done**:
- Added `DeriveTopSpeciesAsync()` in `MycosoftController`:
  - Queries last 7 days of events (limit 500)
  - Extracts `References.Taxonomy.Species`, falls back to
    `References.Taxonomy.ScientificName`
  - Filters out null / empty / "unknown"
  - Groups case-insensitively, orders by frequency, takes top 10
- 8 unit tests cover the derivation logic.

**Remaining work**: None for the code change. Quality of results depends on
whether MINDEX events actually carry populated taxonomy fields. If the upstream
ingestion pipeline doesn't set `References.Taxonomy`, this will correctly
return an empty array rather than fabricated data.

---

### #6 — Support config updates for MycoBrain-only devices without 404 ✅ FIXED

**Problem**: `DeviceService.UpdateDeviceAsync` used `ReplaceItemAsync`, which
returns HTTP 404 when the device document doesn't exist in the `devices`
container — breaking MycoBrain-only devices that haven't been pre-registered
through the main device flow.

**What was done**:
- Changed `ReplaceItemAsync` → `UpsertItemAsync` in `DeviceService`
- Added `CreatedAt` backfill for first-time upserts
- Removed the `catch (CosmosException … NotFound)` block that threw
  `InvalidOperationException` — the upsert path never returns 404
- Note: `MycoBrainService.UpdateDeviceAsync` already used `UpsertItemAsync`;
  this fix brings the generic `DeviceService` into alignment.

**Remaining work**: None for this repo.

---

### #5 — Provision MYCA_API_URL for live MYCA query responses ✅ CODE FIXED / ⚠️ INFRA PENDING

**Problem**: `ProcessMycaQueryAsync` always returned synthetic/hardcoded
responses regardless of whether a live MYCA backend existed.

**What was done (code)**:
- `MycosoftIntegrationService.ProcessMycaQueryAsync` now checks
  `MYCA_API_URL` env var at runtime:
  - **Set** → calls `POST {MYCA_API_URL}/query` with the enhanced query
    and user ID, returns live response with `Synthetic = false`
  - **Unset / empty** → falls back to the existing synthetic generator
    with `Synthetic = true`
  - **Live endpoint fails** → gracefully falls back to synthetic
- Added `Synthetic` boolean to `MycaResponse` so consumers can
  distinguish live vs. fallback answers.
- Added `MYCA_API_URL=` to `.env.example`.

**Remaining work (infrastructure — not in this repo)**:
1. Deploy the live MYCA backend service (likely in `MycosoftLabs/website`
   or a dedicated MYCA repo)
2. Set `MYCA_API_URL` in Azure App Service configuration for
   staging and production slots
3. Validate that responses return `Synthetic = false`

This cannot be completed solely in the NatureOS repo because the MYCA
backend is an external service whose provisioning is an infrastructure /
ops task.

---

## Additional fixes in this PR

| Area | Change |
|------|--------|
| **README** | Fixed broken relative link `../../WEBSITE/website/docs/SYSTEM_ARCHITECTURE.md` → points to website repo. Corrected misleading claim that NatureOS "runs as part of" the website. Added links to existing docs. |
| **CI workflow** | Restored full solution (`NatureOS.sln`) instead of single project, so all projects build before deploy. |

---

## Open PRs (not touched)

| PR | Status | Notes |
|----|--------|-------|
| [#12](https://github.com/MycosoftLabs/NatureOS/pull/12) | Draft | Security CVE-2026-31431 bundle — separate concern, not merged |
| [#4](https://github.com/MycosoftLabs/NatureOS/pull/4) | Open | Fix integration truthfulness — separate concern, not merged |

---

## Summary

All five issues (#5–#9) have code-level fixes in this PR.
Issue #5 additionally requires an infrastructure step (provisioning the
MYCA_API_URL environment variable in Azure once the backend exists).
No other issues are blocked on work in this repository.
