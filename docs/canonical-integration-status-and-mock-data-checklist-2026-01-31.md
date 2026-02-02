# 2026-01-31 Canonical Integration Status and Mock-Data Removal Checklist

## Purpose
Provide a single, dated source of truth for MycoBrain integration status across
Mycosoft MAS and Website (with the MycoBrain repo as the canonical firmware and
protocol reference), and enumerate concrete mock-data removal tasks mapped to
paths and endpoints.

This report is strictly real-data only: no mock, simulated, or randomized data
is acceptable in production surfaces.

## Canonical sources reviewed (last 30 days, newest first)
### Mycosoft MAS
- docs/PERSONAPLEX_DEPLOYMENT_JAN29_2026.md
- docs/PERSONAPLEX_INTEGRATION_COMPLETE_JAN29_2026.md
- docs/PERSONAPLEX_COMPLETE_SETUP_JAN29_2026.md
- docs/PERSONAPLEX_NATIVE_FIX_JAN29_2026.md
- docs/PERSONAPLEX_WORKING_JAN29_2026.md
- docs/VOICE_FULL_DUPLEX_WORKING_JAN29_2026.md
- docs/VOICE_SYSTEM_FIX_JAN29_2026.md
- docs/PERSONAPLEX_INTEGRATION_JAN27_2026.md
- docs/N8N_INTEGRATION_STATUS_JAN27_2026.md
- docs/MAS_IMPLEMENTATION_COMPLETE_JAN27_2026.md
- docs/METABASE_N8N_INTEGRATION_JAN27_2026.md
- docs/MAS_TOPOLOGY_*_JAN26_2026.md
- docs/TOPOLOGY_CONNECTION_SYSTEM_JAN26_2026.md
- docs/MYCOBRAIN_CONNECTION_REPORT_JAN23_2026.md
- docs/MYCOBRAIN_TROUBLESHOOTING_GUIDE.md
- docs/MYCOBRAIN_FIX_JAN20_2026.md
- docs/MYCOBRAIN_SETUP_COMPLETE.md
- docs/MYCOBRAIN_SETUP_FINAL_STATUS.md
- docs/MYCOBRAIN_SETUP_INSTRUCTIONS.md
- docs/MYCOBRAIN_HARDWARE_CONFIG.md
- docs/MYCOBRAIN_ARCHITECTURE.md

### Website
- docs/SYSTEM_INTEGRATION_COMPLETE_JAN25_2026.md
- docs/SYSTEM_STATUS_JAN25_2026.md
- docs/DEPLOYMENT_REPORT_FINAL_2026-01-25.md
- docs/MAS_TOPOLOGY_CONNECTION_SYSTEM_JAN26_2026.md
- docs/CREP_SYSTEM.md
- docs/CREP_DASHBOARD_GUIDE.md
- docs/EARTH_SIMULATOR_STATUS.md
- docs/EARTH_SIMULATOR_IMPLEMENTATION_SUMMARY.md
- docs/EARTH_SIMULATOR_ERRORS_AND_FIXES.md
- docs/MYCOBRAIN_GUIDE.md
- docs/MYCOBRAIN_FIXES_2026-01-15.md
- docs/MYCOBRAIN_SENSOR_LIBRARY.md
- docs/MYCOBRAIN_FULL_INTEGRATION.md
- docs/MYCOBRAIN_INTEGRATION_COMPLETE.md
- docs/MYCOBRAIN_WIDGET_INTEGRATION.md
- CHANGELOG_MYCOBRAIN.md
- MYCOBRAIN_CONNECTION_FIX.md

### MycoBrain repo
- docs/MYCOSOFT_ECOSYSTEM_LEARNING_SUMMARY.md
- README.md

### NatureOS (this repo, last 72 hours)
- docs/natureos-integration-master-plan-2026-01-31.md
- docs/earth-2-personaplex-integration-report-2026-01-31.md

---

## Canonical integration status (as of 2026-01-31)

### 1) MycoBrain hardware and firmware (MycoBrain repo is canonical)
- Protocol: MDP v1 (COBS framing + CRC16, ack/nack, endpoints defined).
- Hardware: dual ESP32-S3 (Side-A sensors, Side-B routing), LoRa SX1262,
  dual BME688 (0x76/0x77), NeoPixel on GPIO15, buzzer on GPIO16.
- Firmware guidance and pin maps are authoritative in MycoBrain repo docs.

### 2) MycoBrain service and connectivity (MAS + Website)
- MAS-side MycoBrain service is operational in the documented Windows host
  workflow, bridging COM7 to sandbox via `MYCOBRAIN_SERVICE_URL`.
- Latest connectivity report indicates a working chain:
  Browser -> Cloudflare -> VM (website) -> MycoBrain service (Windows host) -> USB.
- Current hardware state (latest report):
  - Device connected and API endpoints responding.
  - BME688 subscription failures persist (investigate BSEC2).

### 3) Website integration (device manager + CREP + Earth Simulator)
- Device Manager fixes are applied in Website repo:
  - Correct device_id resolution (port vs device_id).
  - Correct command format for MycoBrain service.
  - Machine mode initialization corrected and auto-initialized on connect.
- CREP is fungal-first with MycoBrain devices and fungal observations primary.
  Transport layers are labeled demo and off by default.
- Earth Simulator (Cesium) is operational for fungal and device layers, with
  grid system and API routes in place. Optional layers (mycelium/heat/weather)
  are staged but not backed by real tiles yet.

### 4) MAS orchestration and voice (PersonaPlex + n8n)
- PersonaPlex full-duplex voice is integrated and deployable (GPU/CPU-offload).
- n8n workflows are operational on MAS VM; website routes target
  `/webhook/myca/command`.
- MAS topology dashboard is functional with connection persistence and tools;
  real-metric integration remains incomplete in some parts.

---

## Mock-data removal checklist (real data only)

### A) MAS Topology metrics and simulated responses (Website repo)
**Problem:** Topology docs reference seeded-random metrics and simulated
responses when MAS orchestrator is offline.

**Required actions (no mock data):**
1) Replace seeded-random metrics with live metrics from MAS orchestrator.
2) If MAS is offline, return an explicit "unavailable" status and empty data.

**Likely paths and endpoints:**
- website/app/api/mas/topology/route.ts
- website/app/api/mas/orchestrator/action/route.ts
- website/components/mas/topology/advanced-topology-3d.tsx
- website/components/mas/topology/topology-tools.tsx

### B) CREP transport fallback data (Website repo)
**Problem:** CREP docs mention fallback/sample data for maritime layers.

**Required actions (no mock data):**
1) Remove sample/fallback datasets for AIS/transport.
2) If live feed unavailable, return an error or "no data" with clear status.

**Likely paths and endpoints:**
- website/app/api/oei/aisstream/route.ts
- website/app/api/oei/flightradar24/route.ts
- website/app/api/oei/celestrak/route.ts
- website/services/crep-collectors/*

### C) CREP demo layers (Website repo)
**Problem:** Demo layers are labeled and may be enabled without verified live data.

**Required actions (no mock data):**
1) Keep demo layers disabled unless live feeds are configured and verified.
2) Add explicit status messaging when a layer is disabled or unavailable.

**Likely paths:**
- website/docs/CREP_SYSTEM.md (documentation alignment)
- website/components/crep/* (layer toggles, status indicators)

### D) Earth Simulator optional tiles (Website repo)
**Problem:** Optional layers (mycelium/heat/weather/NDVI/NLM) are not backed by
real tile generation yet.

**Required actions (no mock data):**
1) Do not generate synthetic tiles.
2) Return 501 (Not Implemented) or explicit "data unavailable" where appropriate.
3) Keep layers disabled until real data sources are connected.

**Likely paths and endpoints:**
- website/app/api/earth-simulator/mycelium-tiles/* (if present)
- website/app/api/earth-simulator/heat-tiles/* (if present)
- website/app/api/earth-simulator/weather-tiles/* (if present)
- website/components/earth-simulator/* (layer toggle defaults)

### E) Any remaining placeholder metrics (NatureOS repo)
**Problem:** Placeholder or randomized outputs are disallowed.

**Required actions (no mock data):**
1) Replace placeholders with real measurements or return "unavailable."
2) Ensure errors are explicit and consistent (no random values).

**Likely areas:**
- Any endpoint returning "simulated," "seeded," or "randomized" values.

---

## Next verification steps (real data only)
1) Confirm MycoBrain device telemetry is live at
   `https://sandbox.mycosoft.com/api/mycobrain/health` and `/devices`.
2) Confirm MAS orchestrator health via `http://192.168.0.188:8001/health`.
3) Confirm CREP layers show only live data sources (no fallback datasets).
4) Confirm Earth Simulator optional layers remain disabled until real tiles exist.

---

## Open decisions needed
1) Canonical endpoint for MAS -> NatureOS integration:
   `/api/*` vs MAS compatibility endpoints.
2) Authoritative sensor field mapping for generic readings.
3) Final decision on disabling demo layers until live feeds are configured.

---

Document created: 2026-01-31
