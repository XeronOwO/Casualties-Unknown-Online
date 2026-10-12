# Acceptance record — Global adaptive report-rate flow control — Stage 1: stream taxonomy + health-driven overwrite governor

- Ticket: `global-adaptive-report-rate-stage-1-global-governor` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket's "What landed" section is prose, so this run writes one row per landed claim and judges each
against this run's own suite output and the committed tree.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The reliable-vs-unreliable split is explicit at the stream level: `AdaptiveStreamDeliveryMode` carries `ReliableControl`, `LatestWins` and `Cumulative`, and no adaptive stream is reliable-control | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamCatalogTests.EveryAdaptiveStream_HasLossTolerantDeliveryMode` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.ReliableControl_IsNeverThrottled` — `Passed` (a reliable-control frame is never rate-adapted) |
| 2 | `AdaptiveStreamId` + `AdaptiveStreamProfile` + `AdaptiveStreamCatalog` declare the four Stage 1 streams (`PlayerStateBroadcast`, `PlayerStateReport`, `EnemyStateBroadcast`, `TutorialClawBroadcast`) as `LatestWins` | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamCatalogTests.Catalog_ContainsKnownStreams` — `Passed`; `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamCatalog.cs`, read in this run, holds the four entries ("[AdaptiveStreamId.PlayerStateBroadcast] = new("), each with `AdaptiveStreamDeliveryMode.LatestWins`, a min/max Hz range and a priority |
| 3 | `AdaptivePressureClassifier` maps RTT, jitter and probe-loss thresholds to Optimal / Moderate / High / Critical | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptivePressureClassifierTests.RttThresholds_AreExact(averageRttMs: 400, expected: Critical)` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptivePressureClassifierTests.LossThresholds_AreExact(lossPercent: 40, expected: Critical)` — `Passed` (the boundary cases at 99/100/200/400 ms, 29/30/60/100 ms jitter and 7.9/8/20/40 percent loss are green in the same run) |
| 4 | `AdaptiveRatePolicy` is a pure mapping from pressure + priority to effective Hz, and only `LatestWins` streams are adapted — reliable-control streams keep the exact configured cadence. The ticket's trailing clause "and cumulative" is judged as **superseded**, not as written: `Cumulative` is adapted from Stage 3 on (`CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.Cumulative_IsAdaptedInStage3`, judged in that stage's own record in this batch) | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.LatestWins_Pressure_LowersEffectiveHz(pressure: Critical)` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.ReliableControl_IgnoresAdaptiveBounds` — `Passed` (and `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.HigherPriority_DegradesLessThanLowerPriority`, `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.LatestWins_HighPressure_DoesNotGoBelowMinHz` are green in the same run) |
| 5 | One shared `AdaptiveStreamRateService` answers "what cadence should this stream use right now" — per-peer and worst-peer broadcast queries, send intervals, and an effective-cadence log line per change | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.Broadcast_UsesMostConstrainedPeer` — `Passed` (with `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.Unicast_UsesPerPeerHealth`, `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.EmptyBroadcast_IsOptimalDefault` and `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.UnknownStream_ReturnsConfiguredBase` green in the same run); the same class logs each change: `src/CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync/AdaptiveStreamRateService.cs` — "[AdaptiveSync] {Stream} peer {Peer} cadence {Previous}Hz -> {Effective}Hz (pressure {Pressure})." |
| 6 | The three overwrite streams schedule from the service instead of reading `StateStreamOptions` directly: `EntitySyncService`, `EnemySyncService` and `TutorialClawService` follow the adaptive cadence | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateIntegrationTests.HighPeerRtt_ReducesLossTolerantStreamCadence` — `Passed` (over the suite's fake network, a high-RTT peer lowers the player/enemy/tutorial loss-tolerant cadence while the stream keeps flowing) |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs or of the committed tree, and no
row asks for a person's judgement. The ticket's own closing note stands — a real dual-client pass remains
the user's final unified acceptance, which this offline batch neither performs nor substitutes for.

## Limits

- **No client was started.** No rendering, frame, live log or probe was observed; rows 1–4 and 6 are
  decided by this run's test output and rows 2 and 5 additionally by inspection of the committed source.
  Row 5's log line is the committed emitter, not a line read from a running session.
- **The historical suite numbers were not re-measured.** The ticket states "2417 + 16 normative gates
  passed"; this run measured 626 normative-gate cases and 5072 behavioural cases, both 0 failed / 0 skipped
  (`gates.log`, `full.log`). The 2417 figure is the writing cycle's measurement.
- **The ticket's self-check bullet is a process record, not a row here.** "Independent adversarial
  self-check: completed by a fresh subagent" was run by the writing cycle; this batch did not re-run it.
  What the rows read is the durable outcome of the fixes it names — the focused cases above are green in
  this run.
- **One line of the ticket is already superseded.** Its "Cumulative is defined but not adapted in Stage 1"
  no longer holds: Stage 3 landed cumulative adaptation, whose case
  (`CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.Cumulative_IsAdaptedInStage3`) is green in this run and is judged in that
  stage's own record.
- **This run judges the mechanism, not the wire.** The ticket's "no wire-protocol changes" is a statement
  about what did not change; it is not a row and is not measured here.
