# MycoBrain â†” NatureOS Integration

This repo includes first-class integration for **MycoBrain V1** (dual ESP32-S3 + SX1262 LoRa), ingesting **MDP v1** telemetry into **MINDEX** and exposing control + dashboards through **NatureOS**.

## Whatâ€™s implemented

- **MDP v1 decode/encode** (COBS + CRC16): `src/mycorrhizae/MDPv1Protocol.cs`
- **MINDEX models** (telemetry/device/command): `src/mindex/Models/MycoBrainModels.cs`
- **Core API endpoints**: `src/core-api/Controllers/MycoBrainController.cs`
- **Core API processing service** (idempotent storage + event fan-out): `src/core-api/Services/MycoBrainService.cs`
- **Ingestion functions** (Service Bus â†’ Cosmos/EventGrid): `src/ingestion/MycoBrainIngestionFunction.cs`
- **Dashboard widget**: `src/dashboard/components/MycoBrainWidget.tsx`

## Data flow

1. **MycoBrain Sideâ€‘B** transports frames (LoRa / UART gateway)
2. Gateway/MAS publishes **NDJSON** or **binary MDP frames** to Service Bus
3. NatureOS ingests and stores:
   - `mindex/events` (MycorrhizaeEvent)
   - `mindex/mycobrain_telemetry` (raw MycoBrainTelemetry)
   - `mindex/devices` (device registry)

## Core API endpoints

- **Telemetry ingest**: `POST /api/mycobrain/telemetry`
- **NDJSON ingest**: `POST /api/mycobrain/telemetry/ndjson`
- **Binary MDP ingest**: `POST /api/mycobrain/telemetry/mdp`
- **Send command**: `POST /api/mycobrain/command`
- **Devices**:
  - `POST /api/mycobrain/devices/register`
  - `GET /api/mycobrain/devices`
  - `GET /api/mycobrain/devices/{serial}`
  - `PUT /api/mycobrain/devices/{serial}`
  - `GET /api/mycobrain/devices/{serial}/telemetry`

## Required Azure resources

- Cosmos DB (database: `mindex`)
  - container `events` (partition key: `source_device`)
  - container `mycobrain_telemetry` (partition key: `serial`)
  - container `devices` (partition key: `device_id`)
- Service Bus queues
  - `mycobrain-telemetry` (NDJSON)
  - `mycobrain-mdp-frames` (binary frames)
  - `mycobrain-commands` (downlink)
  - `mycorrhizae-events` (fan-out)

## Dashboard

The dashboard uses `MycoBrainWidget` and expects:

- `NEXT_PUBLIC_API_URL` â†’ Core API base URL (e.g. `https://natureos-api.mycosoft.com`)

