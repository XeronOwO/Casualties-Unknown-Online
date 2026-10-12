# Acceptance record — Global adaptive report-rate / sync-frequency flow control — roadmap

- Ticket: `global-adaptive-report-rate-flow-control` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This roadmap carries no row table of its own: its acceptance claims are the four prose bullets of its
"Acceptance direction" section, so this run writes one row per bullet and judges them against the landed
stages (Stage 1–4, each with its own record in this batch).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Under simulated bandwidth/latency pressure, message volume drops while the final committed state remains identical | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateIntegrationTests.HighPeerRtt_ReducesLossTolerantStreamCadence` — `Passed` in this run (a high-RTT peer lowers the loss-tolerant streams' cadence, i.e. fewer frames on the wire); `CasualtiesUnknownOnline.Tests.Session.MedicalOperationSessionServiceTests.Guest_InjectionStartUpdateEnd_CarriesEachDoseOnceAndEmitsSingleEnd` — `Passed` (each dose reaches the host exactly once, so the fewer frames carry the same committed amount). The "identical" half is carried by that exactly-once delivery, not by a state-equality comparison — no case in this tree compares pre- and post-pressure committed state, and `Limits` says so |
| 2 | No regression in frame-level/optimal mode when no pressure is detected | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.LatestWins_Optimal_KeepsConfiguredBaseHz` — `Passed` (optimal pressure returns the configured base cadence unchanged); `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.MedicalStreams_UseProfileBaseHzInOptimal` — `Passed` (the frame-level 60 Hz medical base is kept in optimal mode) |
| 3 | A new stream can opt in with small, declarative configuration rather than a domain-specific duplicate implementation | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamCatalogTests.Catalog_ContainsKnownStreams` — `Passed` (streams opt in as catalog profiles, not per-domain code); `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.EveryCatalogStream_IsMappedByWireMapper` — `Passed` (every catalogued stream is mapped to its wire observation key) |
| 4 | Metrics observable in logs/telemetry | `machine` | **pass** | `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamRateService.cs`, read in this run, logs the cadence metric per stream and peer: "[AdaptiveSync] {Stream} peer {Peer} cadence {Previous}Hz -> {Effective}Hz (pressure {Pressure})." (and the interval equivalent in `LogIntervalChange`); the values behind it are the traffic estimates of `NetworkTrafficMonitor`, whose per-peer/per-payload windows the gate suite covers in this run |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs or of the committed tree, and no
row asks for a person's judgement. The ticket's own closing note stands as it was written — a real
dual-client pass remains the user's final unified acceptance, which this offline batch neither performs
nor substitutes for.

## Limits

- **No client was started.** Nothing in this record is a rendering, frame, live-log or probe observation;
  row 4 is grounded on the committed emitter and on this run's test output, not on a log line read from a
  running session.
- **"Under pressure" is the suite's simulated pressure.** Row 1's pressure comes from the behavioural
  suite's fake-network traffic and peer-health fixtures, not from a real degraded link. Its state half is
  carried by the exactly-once delivery case above; no case compares the committed state before and after the
  pressure, and this record does not claim such a comparison.
- **The roadmap is judged through its stages.** Rows 1–3 are grounded on the Stage 1–4 mechanisms whose own
  records name their focused cases; this record does not re-enumerate them.
- **Stage 1's historical suite numbers were not re-measured** (the ticket states "2417 + 16 normative
  gates"). This run measured its own suites: 626 normative-gate cases and 5072 behavioural cases, both
  0 failed / 0 skipped (`gates.log`, `full.log`); the historical figure is that cycle's measurement.
