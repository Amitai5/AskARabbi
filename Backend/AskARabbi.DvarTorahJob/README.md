# Weekly Dvar Torah Container Apps Job

This .NET 10 executable is the isolated weekly write path for the `WeeklyAIDvarTorahs` MongoDB collection. Its Docker image is built for the `askarabbi-weekly-dvar-torah` Azure Container Apps Job, whose five-field cron expression is `5 8 * * 0` (Sunday at 08:05 UTC). Each execution performs at most one generation attempt and exits; it does not run an internal timer or HTTP server.

The job calculates the upcoming Shabbat with the same pinned calendar service as the API, acquires a recoverable MongoDB lease, calls `IWeeklyDvarTorahGenerator`, and atomically publishes once. Platform retries either return `AlreadyPublished`, observe `GenerationInProgress`, or recover an expired lease.

## Safe pre-generation state

`DvarTorah__GenerationEnabled` defaults to `false`. In that state the scheduled container writes one structured `WeeklyDvarTorahGenerationDisabled` log and exits successfully without reading MongoDB configuration or constructing a client. This lets the image, schedule, identity, and deployment path exist before the content contract is approved.

`UnconfiguredWeeklyDvarTorahGenerator` remains an explicit failure boundary. Before setting `DvarTorah__GenerationEnabled=true`, replace it in `JobDependencyFactory` with the approved source, prompt, validation, and model implementation and complete the activation checklist in [`docs/PRODUCTION_READINESS.md`](../../docs/PRODUCTION_READINESS.md).

## Runtime configuration

| Environment variable | Required | Default |
| --- | --- | --- |
| `DvarTorah__GenerationEnabled` | No | `false` |
| `MongoDB__ConnectionString` | Only when generation is enabled | None; configure as a Container Apps secret reference |
| `MongoDB__DatabaseName` | No | `askarabbi` |
| `MongoDB__DvarTorahCollectionName` | No | `WeeklyAIDvarTorahs` |
| `DvarTorah__InIsrael` | No | `false` (Diaspora cycle) |
| `DvarTorah__GenerationLeaseMinutes` | No | `30` |

The MongoDB connection string must remain in the Container Apps Job secret store or an Azure Key Vault reference. Do not pass it as a build argument, commit it to configuration, or print it to logs.

## Build and local verification

Run from the repository root:

```powershell
dotnet run --project Backend/AskARabbi.DvarTorahJob
docker build --file Backend/AskARabbi.DvarTorahJob/Dockerfile --tag askarabbi-dvar-torah-job:local .
docker run --rm askarabbi-dvar-torah-job:local
```

Both local runs use the safe disabled default and should exit with code `0`. The production workflow builds this Dockerfile, pushes `askarabbi-dvar-torah-job:<verified-commit>` to ACR, resolves its immutable digest, updates the existing Container Apps Job, and verifies the job image, schedule trigger, cron expression, and provisioning state.
