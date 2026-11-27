# AWS infrastructure reference and legacy file register

Verified 2026-11-27; account 376129856177, us-east-1. Production servers are AWS-only. This source branch is not deployed. Canonical system map: MycosoftLabs/mycosoft-mas root `MYCOSOFT_AWS_INFRA_CURRENT.md`.

Source base: `a345c632a826cad180f2fd39763d9952ec0c74a2`. Supplied Grok patches are rebased by exact hunk context/three-way merge; unavailable MycoBrain base ef93c57 was reconciled against current main using complete matching context. Historical code bodies remain present.

## Configuration

MINDEX: `http://mindex-internal.mycosoft.com:8000`; MAS: `http://mas-internal.mycosoft.com:8001`; MycoBrain: `http://mycobrain-internal.mycosoft.com:8003`. Mycorrhizae uses `http://internal-mycosoft-core-int-21300503.us-east-1.elb.amazonaws.com:8002`; n8n MAS/MYCA use the same internal ALB ports5678/5679. Website is `https://mycosoft.com`; Origin is `https://origin-aws.mycosoft.com:8443`. These private backends are server-side only. Never put service secrets into NEXT_PUBLIC variables. Database access uses Secrets Manager application credentials and the TLS-verified RDS Proxy; see the canonical map.

Unknown optional LAN/hypervisor/device integrations need explicit configuration and separate verification; a retained literal in those bodies is not a discovered AWS replacement. Tests, source datasets, migrations and signed records retain evidence. Retired CLI guards only stop the standalone old SSH/Proxmox utility; importable library bodies remain. AWS-aware utilities were preserved. Production AWS services were not restarted or redeployed.

## GitHub workflow inventory

- `.github/workflows/deploy-production.yml`: triggers `push:`, `pull_request:`, `workflow_dispatch:`; runners `ubuntu-latest`; actions `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`.

## Complete matching-file register

Rows are a reviewed inventory, not evidence that an optional connector is used in production. Each replacement either points to the verified AWS service, retires the local topology instruction, or preserves a non-current evidence/configuration record.

| File | Disposition | Replacement / retained-reference meaning |
|---|---|---|
| `.env.example` | AWS / AWS_ENDPOINTS_CORRECTED | http://192.168.0.189:8000 -> http://mindex-internal.mycosoft.com:8000 |
| `docs/MANUAL_RELEASE_POLICY_SEP30_2026.md` | RET / LEGACY_DOCUMENT_CORRECTED | Use the AWS service/Secrets Manager configuration matrix; no LAN/VM/NAS production target. Retained text is historical, fixture data or an optional external connector. |
| `docs/natureos-integration-master-plan-2026-01-31.md` | RET / RETIRED_LOCAL_REFERENCE_OR_OPTIONAL_EXTERNAL_CONNECTOR | Retired local topology; use the verified AWS service map. No AWS counterpart is asserted for physical device fixtures, archival records, or optional hypervisor/network connectors. |
| `src/core-api/Program.cs` | AWS / AWS_ENDPOINTS_CORRECTED | http://192.168.0.187:3000 -> removed from browser CORS defaults; http://192.168.0.188:8001 -> removed from browser CORS defaults; http://192.168.0.189:8000 -> removed from browser CORS defaults |
| `src/core-api/Services/DeepAgentEventService.cs` | AWS / AWS_ENDPOINTS_CORRECTED | http://192.168.0.188:8001 -> http://mas-internal.mycosoft.com:8001 |
| `src/core-api/Services/MycoBrainService.cs` | AWS / AWS_ENDPOINTS_CORRECTED | http://192.168.0.189:8000 -> http://mindex-internal.mycosoft.com:8000 |
