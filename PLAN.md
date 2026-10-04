# NatureOS Issues #5–#9 — Resolution Plan (2026-08-31)

## Repository scope

NatureOS is a .NET 8 backend API deployed to Azure App Service `natureos-api-prod001`.
The solution (`NatureOS.sln`) contains four runtime projects (CoreApi, MINDEX models,
Mycorrhizae protocol, Ingestion functions) and one test project (NatureOS.Tests).

**What lives here**: core API, MINDEX event/device models, MDP protocol codec,
Azure Functions ingestion, sensor firmware, MATLAB scripts, infrastructure-as-code.

**What does NOT live here**: the Mycosoft Website (Next.js, in `MycosoftLabs/website`),
MYCA AI backend, MAS orchestration, Fusarium digital-twin domain logic.
The two `/api/mycosoft/fusarium/*` endpoints in this repo are thin bridge proxies
that reuse standard dashboard data — no Fusarium-specific models or services exist in `src/`.

---

## Issue-by-issue status

### #9 — Run NatureOS unit tests in CI ✅ FIXED

**Problem**: No test project existed. CI ran `dotnet test` on the CoreApi project, which
discovered zero tests and silently passed.

**What was done**:
- Created `tests/NatureOS.Tests/` with 26 xUnit tests covering TopSpecies derivation
  logic, MycaResponse synthetic flag, MAS health result model, device upsert semantics,
  MycorrhizaeEvent model baseline.
- Added project to `NatureOS.sln`.
- Updated `.github/workflows/deploy-production.yml` to restore the full solution
  and run `dotnet test tests/NatureOS.Tests/NatureOS.Tests.csproj`.
- All 26 tests pass locally on .NET 8.0.

**Remaining**: None for this repo.

---

### #8 — Replace write-based MAS health probe with read-only check ✅ FIXED

**Problem**: `/api/mycosoft/status` reported `MAS = "Unknown"`. The issue described
write-based probing that created `health_check` documents in `mas_context`.

**What was done**:
- Added `IMasIngestionService.CheckHealthAsync()` — a read-only `COUNT` query on `mas_context`.
- Added `MasHealthResult` record with `Healthy`, `Status`, `Detail`.
- `/api/mycosoft/status` now calls `CheckHealthAsync()`, reports `MAS = "Healthy"/"Unhealthy"`
  with detail, and degrades `Overall` to `"Degraded"` when MAS is unreachable.
- No documents are written during the health check.

**Remaining**: None.

---

### #7 — Derive TopSpecies from taxonomy fields ✅ FIXED

**Problem**: `GetSystemContext()` returned `TopSpecies = Array.Empty<string>()`.
The issue said TopSpecies was "approximated from kingdom_domain distribution" but in
practice it was just empty.

**What was done**:
- Added `DeriveTopSpeciesAsync()` in `MycosoftController`:
  - Queries last 7 days of events (limit 500)
  - Extracts `References.Taxonomy.Species`, falls back to `ScientificName`
  - Filters null / empty / "unknown", groups case-insensitively, orders by frequency, top 10
- 8 unit tests cover the derivation logic.

**Remaining**: Quality depends on whether the ingestion pipeline populates
`References.Taxonomy` on events. If upstream events have no taxonomy,
this correctly returns `[]` rather than fabricating data.

---

### #6 — Support config updates for MycoBrain-only devices without 404 ✅ FIXED

**Problem**: Two related bugs:

1. `DeviceService.UpdateDeviceAsync` used `ReplaceItemAsync`, which returns HTTP 404
   from Cosmos when the device document doesn't exist.
2. `MasDevicesController.UpdateDeviceConfig` (`PUT /devices/{id}/config`) had a
   **NullReferenceException**: when the device was found only via MycoBrain (not in
   the `devices` container), `device` remained null but the code accessed `device.Metadata`.

**What was done**:
- Changed `DeviceService.UpdateDeviceAsync`: `ReplaceItemAsync` → `UpsertItemAsync`
  with `CreatedAt` backfill for first-time upserts.
- Fixed `MasDevicesController.UpdateDeviceConfig`: when `device == null` and
  `mycoDevice != null`, the code now maps `mycoDevice` to a `Device` via
  `MapMycoBrainDevice()` before applying config and upserting.

**Remaining**: None for this repo.

---

### #5 — Provision MYCA_API_URL for live MYCA query responses ✅ CODE FIXED / ⚠️ INFRA PENDING

**Problem**: `ProcessMycaQueryAsync` always returned hardcoded synthetic responses
regardless of environment.

**What was done (code)**:
- `MycosoftIntegrationService.ProcessMycaQueryAsync` checks `MYCA_API_URL` at runtime:
  - **Set** → `POST {MYCA_API_URL}/query`, returns `Synthetic = false`
  - **Unset** → existing synthetic generator with `Synthetic = true`
  - **Live endpoint fails** → graceful fallback to synthetic
- Added `Synthetic` boolean to `MycaResponse`.
- Added `MYCA_API_URL` to `.env.example`.

**Remaining (infrastructure — outside this repo)**:
1. Deploy or identify the live MYCA backend service
2. Set `MYCA_API_URL` in Azure App Service configuration for staging/production
3. Validate that responses return `Synthetic = false`

---

## Additional fixes

| Area | Change |
|------|--------|
| **README** | Full rewrite. Was website-centric with broken links (`../../WEBSITE/website/docs/SYSTEM_ARCHITECTURE.md`, `./docs/INTEGRATION_GUIDE.md`). Now accurately describes the .NET repo, solution structure, API routes, CI pipeline, and relationship to other repos. |
| **MasDevicesController NRE** | Fixed null dereference in `PUT /devices/{id}/config` for MycoBrain-only devices (part of #6). |
| **package-lock.json** | Deleted 82-byte placeholder (`{ "name": "wxp", ... }`) — unused in this .NET repo. |
| **CI workflow** | Restores full solution (`NatureOS.sln`) so all projects build before deploy. |
| **Fusarium scope** | Documented in README: Fusarium digital-twin logic is website+MINDEX+MAS, this repo only has two thin proxy endpoints. |

---

## Stale pull requests

| PR | Title | Opened | Status | Notes |
|----|-------|--------|--------|-------|
| [#1](https://github.com/MycosoftLabs/NatureOS/pull/1) | Fix null handling in ingestion and simulation services | 2025-07-29 | Open | Over 1 year old. Branch `codex/check-for-bugs-in-natureos`. Likely conflicts with months of subsequent work. Review for relevance and close or rebase. |
| [#2](https://github.com/MycosoftLabs/NatureOS/pull/2) | Use SignalR for website live updates | 2025-08-08 | Open | Over 1 year old. Branch `codex/optimize-natureos-integration-architecture`. SignalR is now fully implemented in `main` (NatureOSHub, SSE streams). Likely superseded — close. |
| [#4](https://github.com/MycosoftLabs/NatureOS/pull/4) | Fix integration truthfulness and live-data fixes for MAS | 2026-03-28 | Open | 5 months old. May overlap with fixes in this PR. Review separately. |
| [#12](https://github.com/MycosoftLabs/NatureOS/pull/12) | Security: CVE-2026-31431 mitigation bundle | 2026-05-19 | Draft | Security patch — should be reviewed and merged on its own timeline. |

**Recommendation**: Close #1 and #2 as superseded. Review #4 for any remaining value
after this PR merges. #12 is a separate security concern.

---

## Summary

All five issues (#5–#9) have code-level fixes in this PR. Issue #5 additionally
requires provisioning `MYCA_API_URL` in Azure once a MYCA backend exists.
Stale PRs #1 and #2 are likely superseded and should be closed.
