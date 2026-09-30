# Funga environmental filters and document compatibility — September 30, 2026

GET `/api/funga/events` now applies inclusive humidity and pH ranges, in addition to its existing filters. The previous service accepted both ranges but omitted them from SQL. It also queried only the canonical snake-case domain while the two Cosmos writer configurations serialize the DTO differently.

## Supported document formats

The formats below come from `MycorrhizaeEvent` and the two source-backed writer configurations, reproduced with the exact Cosmos SDK3.36.0 serializer implementation and Newtonsoft.Json13.0.3. They are not a census of deployed records.

| Field | Canonical System.Text.Json | Core API Cosmos CamelCase | Ingestion Cosmos default |
|---|---|---|---|
| domain | kingdom_domain | kingdomDomain | KingdomDomain |
| event identity | event_id | eventId | EventId |
| source | source_device | sourceDevice | SourceDevice |
| timestamp | timestamp | timestamp | Timestamp |
| humidity | references.environment.humidity | references.environment.humidity | References.Environment.Humidity |
| pH | references.environment.ph | references.environment.pH | References.Environment.pH |

The service executes three mutually exclusive, parameterized queries. Presence of `kingdom_domain` selects the canonical stream; otherwise `kingdomDomain` selects the core stream; otherwise `KingdomDomain` selects ingestion. Equal domain aliases cannot duplicate a document across streams. The existing source, taxonomy, substrate, temperature, time and mycorrhizal filters use the matching format's paths. A document with arbitrary mixtures outside these complete writer formats is not a new supported storage schema.

Returned DTO fields normalize actual JsonPropertyName, CLR and Newtonsoft camel-case aliases. Equal aliases are accepted; conflicting aliases, malformed typed fields and missing returned timestamps fail the whole request with503 `funga_data_unavailable`, never a partial successful page. Arbitrary payload and dictionary keys are preserved. This is validation of returned rows, not a whole-container integrity scan: provider filters and ORDER BY may exclude malformed records before materialization.

Ranges are inclusive and combine with AND. Missing/null/non-numeric measurements do not match active numeric filters. Unknown nullable measurements remain readable without that filter. Bounds must be finite and ordered. One-sided controller defaults remain humidity0..100 and pH0..14; explicit finite ranges are not clamped to a new physical policy. Invalid bounds or cursors return400 `invalid_funga_query`. Ordinary provider failures retain500.

## Pagination and compatibility limits

Page size remains1..1000. Each query reads at most one head at a time; three heads are merged by the provider's timestamp string order. Ties across formats use canonical, core, ingestion priority; ties within one format retain the provider's continuation order, which is not a new total-order guarantee for mutable data. A bounded Newtonsoft converter preserves raw timestamp strings before date coercion, so fractional precision does not silently reorder a stream. This preserves the existing SQL string-order contract; it does not promise chronological equivalence for mixed time zones or representations. No composite event-id ORDER BY/index is introduced.

The new version1 base64url JSON continuation envelope saves each stream's last consumed position. Unread heads replay on the next request. The fingerprint binds semantic filters and effective sort direction; page size may change. It is an integrity-consistency check, not a signature, authorization control or tamper-proof credential. Never use the token to authorize data access. Old single-stream Cosmos tokens return400 and require query restart.

A request has a budget of `3 * (PageSize + 1) + 32` provider reads; empty provider pages consume this budget. Exhaustion, overlarge provider pages or an envelope exceeding65536 characters return503. Reading one item per stream can cost more requests/RUs; measure before rollout. No items or credentials are embedded in the envelope, but provider cursor metadata is opaque and should not be logged casually. Inserts/updates/deletes between requests can change results. There is no point-in-time snapshot or exactly-once pagination promise.

## Offline reproduction

The regression project links the actual service, controller and model to a small strict SQL/provider fixture. It is not the Cosmos engine or an emulator. Fixtures are explicitly synthetic and generated from the actual DTO using the three writer serializers. No SDK client or HTTP request is issued. The fixture rejects attempted HttpClient traffic. NuGet sources are cleared; use an already available .NET8 SDK and declared-compatible Newtonsoft.Json13.0.3 assembly.

PowerShell, from repository root, with `$jsonAssembly` pointing to that DLL and `$buildRoot` pointing to an owned scratch directory:

```powershell
$project = './tests/FungaFilterRegression/FungaFilterRegression.csproj'
$properties = @("-p:BaseIntermediateOutputPath=$buildRoot/obj/", "-p:NewtonsoftJsonAssembly=$jsonAssembly", '-p:UseAppHost=false', '-p:NuGetAudit=false')
dotnet restore $project --configfile ./tests/FungaFilterRegression/NuGet.Config --disable-parallel @properties
dotnet build $project --no-restore --disable-build-servers -p:UseSharedCompilation=false --output "$buildRoot/bin" @properties
dotnet exec "$buildRoot/bin/FungaFilterRegression.dll"
```

The baseline ran35 cases:33 failed and2 passed. The repair passed the same35 plus17 additional edge cases,52 total. Additional cases cover nested/domain conflicts, equal numeric aliases, within-stream ties, page-size changes, fingerprint mismatch, bounded empty pages, oversized provider responses, parameter injection and payload preservation, exact fractional timestamp ordering and raw timestamp deserialization. The final harness also implements IDisposable on its iterator and preserves raw date strings to match the production SDK/read contract. The35 baseline assertions were rerun against the frozen original source with the final fixture seam:33 failed/2 passed again. Earlier logs are preserved.

Separately, all affected production source compiled against the real pinned Cosmos3.36.0 package DLLs. That compile does not execute the SDK client. The exact serializer-source proof executed offline. Full application build, live Cosmos SQL execution, deployed indexes, partitioning, writer ingestion and mutable-data behavior are not certified by these results. No deployment, data migration, real DB read/write or credential operation occurred.

## Release gate and rollback

Before deployment, run the three exact generated queries against an authorized disposable Cosmos dataset serialized by both real writers and the canonical contract. Verify numeric/type predicates, `/timestamp` and `/Timestamp` ordering indexes, cross-partition continuation/empty pages, aliases, ties and request cost; establish timestamp representation and authorization/partition policy. Existing unrelated Funga analysis/classification stubs are unchanged and not qualified here.

The release combines this bounded change with separately qualified workflow repairs. GitHub publication and live deployment must be checked against the release receipt. Revert this bounded patch in that release branch to restore the old reader; active version1 cursors then require restart. Do not reset or overwrite unrelated workflow repairs. Patch forward/reverse checks and exact before/after hashes are recorded in the accompanying batch8 audit artifacts.
