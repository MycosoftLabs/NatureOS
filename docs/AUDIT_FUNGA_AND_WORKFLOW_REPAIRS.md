# NatureOS audit repair release — September 30, 2026

This release corrects two misleading API behaviors: workflow execution reported completion without an executor, and Funga event searches accepted humidity/pH filters without applying them. The changes are integrated on upstream `b5d22b4b2f4d7149634a74d0c1c18ab2ea97d415`. Source qualification, GitHub publication, main integration and live deployment are separate states.

## Resulting behavior

Workflow definitions can still be created and updated in the existing in-memory service. Execution now returns HTTP 501 with `Success=false`, no invented execution ID, no completed timestamp and no completion event or history. `CompletedAt` is nullable. This repairs truthfulness; it does not implement a workflow executor or durable definition storage. Clients must preserve the unsupported response instead of displaying success.

Funga reads apply inclusive environmental bounds, validate finite ordered ranges and support all three document formats produced by the inspected serializers. Pagination uses bounded queries and explicit continuation state. Invalid input/cursors return 400; incompatible returned documents return a sanitized 503. The detailed [Funga compatibility and operations guide](BATCH8_FUNGA_FILTER_COMPATIBILITY_SEP30_2026.md) explains field names, timestamp ordering, cursor restart, test reproduction, request-cost implications and outstanding Cosmos qualification.

## Validation

On the integrated release checkout, the workflow regression project passes all 10 cases, including the actual controller and event publisher with a recording HTTP handler. The Funga project passes all 52 cases against the actual service/controller/model and a strict provider fixture. The Funga source also compiled against the actual pinned Cosmos SDK in the author package. These are 62 distinct bounded offline cases; no live cloud client, credential or production record was used.

The workflow fixture needs an existing .NET 8 SDK. From the repository root, with `$buildRoot` set to an owned scratch directory:

```powershell
$project = './tests/WorkflowUnavailableRegression/WorkflowUnavailableRegression.csproj'
$properties = @("-p:BaseIntermediateOutputPath=$buildRoot/obj/", '-p:UseAppHost=false', '-p:NuGetAudit=false')
dotnet restore $project --configfile ./tests/WorkflowUnavailableRegression/NuGet.Config --disable-parallel @properties
dotnet build $project --no-restore --disable-build-servers -p:UseSharedCompilation=false --output "$buildRoot/bin" @properties
dotnet exec "$buildRoot/bin/WorkflowUnavailableRegression.dll"
```

Funga reproduction is in the linked guide and additionally requires the declared-compatible Newtonsoft.Json assembly. The isolated projects do not prove a successful full application build, deployed index/partition behavior or end-to-end consumers.

## Deployment and rollback

Before deployment, execute the three Funga queries against a disposable Cosmos dataset produced by the actual writers, inspect required ordering indexes and measure request units. Verify real authenticated consumers preserve 400/501/503, nullable completion time and unknown/unavailable data states. Preserve the previous application image and current configuration for rollback; no data migration is included.

Rollback by reverting these release commits or returning to the previous verified image, preserving unrelated workflow/operator changes. New Funga cursors require a query restart if the old reader is restored. Reverting workflow code restores the misleading success behavior; it does not recover real executions. Do not treat keeping an old image as proof of a tested failover.

## Workflow warning log safety follow-up

The unavailable-executor warning previously included the caller's workflow ID in both rendered text and structured logger state. A workflow ID containing line separators could create misleading log records. The warning now contains only a fixed operational message; workflow IDs and request parameters are omitted. Unsupported execution still returns HTTP 501 with no execution, completion timestamp, event or history.

Two additional offline cases capture the actual service's `ILogger` output. They cover carriage return, newline, escape and Unicode line separators, and verify that neither the rendered warning nor structured fields contain request data. Against main `c944b4fa281c07030d3004bf0dd340519d705017`, the existing 10 cases passed and these two failed; after the one-line service change, all 12 workflow cases pass. Run the same workflow commands above to reproduce. The Funga suite was not rerun for this logging-only follow-up, and no live collector or application deployment was exercised. Closure of CodeQL alert 61 requires a new repository scan; local regression tests are not a CodeQL scan.

The structured-state assertion permits only the logger's single string `{OriginalFormat}` field, equal to the rendered fixed message. Additional fields and nested objects or collections therefore fail the regression, including request data that a top-level `ToString()` comparison would miss.

Rollback this follow-up by reverting only its logging, fixture and documentation changes. That restores the unsafe user-controlled warning content and should not be used as a mitigation; it does not change workflow availability or stored data.
