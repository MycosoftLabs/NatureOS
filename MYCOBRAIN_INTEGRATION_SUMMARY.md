# MycoBrain Integration Notes (NatureOS)

This file is a durable â€œnotes + inventoryâ€ of the MycoBrain integration work.

## Added modules

- **MDP v1 library**: `src/mycorrhizae/MDPv1Protocol.cs`
  - COBS framing + CRC16-CCITT
  - frame encode/decode helpers
  - JSON payload helpers for telemetry/commands

- **MINDEX models**: `src/mindex/Models/MycoBrainModels.cs`
  - telemetry model (Sideâ€‘A + Sideâ€‘B)
  - device schema + status
  - command structure + command IDs

- **Core API**
  - Controller: `src/core-api/Controllers/MycoBrainController.cs`
  - Service: `src/core-api/Services/MycoBrainService.cs`
  - DI registration: `src/core-api/Program.cs`

- **Ingestion**
  - Function: `src/ingestion/MycoBrainIngestionFunction.cs`

- **Dashboard UI**
  - Widget: `src/dashboard/components/MycoBrainWidget.tsx`
  - Wired into: `src/dashboard/app/page.tsx`

## Behavioral guarantees

- **Idempotency**: Telemetry stored into `events` with ID `${serial}-${seq}` and conflict handling.
- **Integrity**: Binary frames validated with CRC16 and COBS decode.
- **Fan-out**: Stored events can be published to Event Grid + Service Bus for downstream MAS processing.

## Known follow-ups

- **AuthN/AuthZ**: add device credentials (API key / certificate) and enforce in ingestion.
- **Command ACK tracking**: persist ACK state and expose command history.
- **Schema hardening**: formalize MycoBrain fields under Mycorrhizae Protocol schema versioning.

