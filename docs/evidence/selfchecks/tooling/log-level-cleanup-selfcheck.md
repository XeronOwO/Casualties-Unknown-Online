# Log-Level Cleanup — Self-Check (2026-08-23)

Backlog §3.6: high-frequency periodic sync paths were logging at Information,
spamming the CUO log during normal play and burying one-shot/error events.
This change moves only the periodic path logs to Debug; join/leave/restore/
refusal/failure logs stay at Information/Warn/Error.

## Changed paths

| Path | Frequency | Old level | New level |
|---|---|---|---|
| `CharacterDataSync` — host `[CloneRender]` char data from guest | 1 Hz per guest | Information | Debug |
| `CharacterDataSync` — guest `[CloneRender]` char-data relay | 1 Hz per peer | Information | Debug |
| `CharacterDataSync` — guest `[CloneRender]` host char data | 1 Hz | Information | Debug |
| `CharacterDataSync` — host broadcasting char data | 1 Hz | Information | Debug |
| `CharacterDataSync` — guest reporting char data | 1 Hz | Information | Debug |
| `FluidSimulationAuthority` — fluid region stream send | periodic region stream (10 Hz diff / 1 Hz full) | Information | Debug |
| `FluidRegionApplication` — fluid region apply | mirror of the stream | Information | Debug |
| `TradeStateSync` — periodic trader-state fallback snapshot | 5 s fallback | Information | Debug |

## Unchanged (deliberately)

- One-shot/error paths in the same classes: restore received/applied, immediate
  inventory-changed re-report, limb event reports, trader action execution/
  rejection, entity spawns/trap replays, world snapshots, warnings.
- `RadiationLineSync` periodic publish was already Debug; the new one-shot
  straggler activation log stays Information (rare, user-visible event).

## Verification

- Build: 0 warnings/0 errors.
- `dotnet format`, `tools/check-architecture.ps1`,
  `tools/check-event-replay.ps1` pass.
- Full L0 test suite still passes (behavior unchanged; log levels only).

## External evidence

- `CharacterDataSync.cs` — 1 Hz reporting/relay paths.
- `FluidSimulationAuthority.cs`, `FluidRegionApplication.cs` — periodic fluid
  region stream.
- `TradeStateSync.cs` — 5 s trader fallback snapshot.
