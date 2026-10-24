# Acceptance — the adapter capability catalog, stage 1: every capability printed on the deployed artifact

- Ticket: `docs/backlog/review/adapter-capability-catalog.md` — verdict: **stage 1's acceptance row passes; the
  ticket stays in `review/`** because stages 2 (degradation) and 3 (install units) are gated on the game update
  that has not landed, exactly as the ticket's own staging says
- Batch: `.acceptance/` (local, gitignored; this run is the `wex*` staging's client, started 2026-10-24)
- Artifact: deployed plugin `0.1.0+5bc7241c…` (the log's own build line; verified before this run)
- Date: 2026-10-24
- Result: **the stage-1 probe prints the whole catalog with each capability's class, contract count and status;
  its report text is the one the behaviour-preservation test pins; both suites are green.**

## The rows

| Stage-1 row | Evidence |
|---|---|
| one probe prints every capability with its class, its contract count and its failure reason | the startup log carries the whole table in one block: `13 capability(ies), 269 patch contract(s) + 9 dynamic row(s); game probe: PlayerCamera/Body/PreRunScript/WorldGeneration: OK`, then **13 per-capability lines** — `[Required] session — 14 contract(s) — OK`, `world` 23, `items` 66, `traps` 36, `medical` 29, `character` 49, `enemies` 15, `crafting` 4, `trading` 11, `save` 2, `tutorial` 3, `[Optional] mods` 23, `[Optional] diagnostics` 3 — and the verdict `session available — no blocking hook failure`. The 11 Required + 2 Optional lines account for all 269 contracts, and a failure would print its reason under the capability (`AdapterCapabilityReportTests.Render_PrintsTheFailureReasonUnderItsCapability`, `Render_PrintsAProbeOnlyFailureWithItsReason`) |
| a test pins that installation behaviour is unchanged | `AdapterCapabilityCatalogTests.ProbeGame_KeepsItsVerdictAndReportText` pins the verdict and the exact report text; the live log's next line is that text verbatim — `Game Adapter: PlayerCamera/Body/PreRunScript/WorldGeneration: OK` — and the install line says `patches installed and verified (269 targets)` |
| the existing suites stay green | the run's full suite: **779 normative gates + 6,395 behaviour tests, 0 failures** (`dotnet test CasualtiesUnknownOnline.slnx`) |

## Limits and substitutions

- The per-capability table is read from the deployed client's own log, not from a dedicated probe call: the
  probe IS that log line (`Game Adapter capability report:`), which is why the run needs nothing beyond the
  client it started.
- Stage 2's synthetic-broken-capability row is not judged here and cannot be: its own staging says it is gated
  on the game update landing, and it additionally needs a player-visible notice that is a user-acceptance item.
  Stage 3 (install units) is downstream of stage 2.
- No second client was used: stage 1's row is a startup property of one client.
