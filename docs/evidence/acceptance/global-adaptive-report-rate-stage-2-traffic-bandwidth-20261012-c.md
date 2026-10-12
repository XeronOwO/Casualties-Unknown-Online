# Acceptance record — Global adaptive report-rate flow control — Stage 2: per-peer/per-stream traffic & bandwidth estimates

- Ticket: `global-adaptive-report-rate-stage-2-traffic-bandwidth` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket's "What landed" section is prose, so this run writes one row per landed claim and judges each
against this run's own suite output.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Per-peer × per-message and per-peer × per-payload send/receive aggregates exist in `NetworkTrafficTracker` / `NetworkTrafficWindow`, a completed window is retained on `NetworkTrafficMonitor`, and a mid-session `Reset()` re-anchors instead of reusing the old window | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Networking.NetworkTrafficTrackerTests.RecordSendAndReceive_AccumulatePerPeerCrossAggregates` — `Passed`; `CasualtiesUnknownOnline.Tests.Networking.PacketTrafficMonitorTests.Update_RetainsLastCompletedWindow_AfterRoll` — `Passed` (with `CasualtiesUnknownOnline.Tests.Networking.PacketTrafficMonitorTests.Reset_ClearsLastCompletedWindow` and `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.ResetMidWindow_DoesNotReuseOldTrafficPressure` green in the same run) |
| 2 | `AdaptiveStreamWireMapper` maps the adaptive stream ids to the wire observation key — `WirePayloadType` for the player/enemy kernel streams, the tutorial-claw message for the claw stream | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.TutorialClaw_MapToDirectMessage` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.KernelStreams_MapToPayloadType(streamId: PlayerStateBroadcast, expected: PlayerStateStream)` — `Passed` (and `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.UnknownStream_ReturnsFalse` in the same run) |
| 3 | `AdaptiveTrafficEstimate` + `AdaptiveTrafficEstimator` compute the per-peer/per-stream bandwidth purely from one traffic window — frames, bytes, bytes per second, failed-send ratio and peer total rates | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveTrafficEstimatorTests.KernelStream_ExtractsPerPeerSendRate` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveTrafficEstimatorTests.MissingStreamForPeer_UsesPeerTotalsAsBandwidthEvidence` — `Passed` (the peer-total fallback the ticket names) |
| 4 | `AdaptivePressureClassifier` accepts an `AdaptivePressureInput` that combines the peer-health snapshot with the traffic estimate, so bandwidth and failed-send thresholds join RTT/jitter/loss as pressure signals | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptivePressureTrafficClassifierTests.PerStreamBandwidthThresholds_AreExact(streamBps: 32768, expected: Moderate)` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptivePressureTrafficClassifierTests.TrafficPressure_TakesWorstWithHealthPressure` — `Passed` (peer-total and failed-send boundaries are green in the same run) |
| 5 | `AdaptiveStreamProfile` gains an optional per-stream byte budget (`MaxBytesPerSecond`), and `AdaptiveRatePolicy` clamps the effective cadence by the measured average frame size and that budget | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.LatestWins_ByteBudget_CapsEffectiveHz` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.LatestWins_ByteBudget_BelowMinHz_ClampsToMinHz` — `Passed` (the no-raise and zero-budget cases are green in the same run) |
| 6 | `AdaptiveStreamRateService` queries the estimator for the peer/stream being adapted and feeds the estimate into classifier and policy; immature (under 1 s) windows are ignored so the join burst creates no false bandwidth pressure | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.ImmatureWindow_DoesNotCreateFalseBandwidthPressure` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.HighPerStreamBandwidth_LowersCadence` — `Passed` (with the failed-send and worst-peer broadcast cases green in the same run) |
| 7 | `EntitySyncService` refreshes the swing hold only while a swing is actually held, so idle frames do not build full traffic snapshots | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AttackSwingStateTests.Tick_AtWindowEnd_ClearsAttacking` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AttackSwingStateTests.SlowStreamHold_KeepsTheFlagAcrossTheLongerCadence` — `Passed` (the hold survives the longer adaptive cadence but not the window's end). The two cases drive the swing-hold state machine the row's first half names; that the idle path therefore builds no full traffic snapshot is read from the ticket and the self-check, exactly as `Limits` says |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs, and no row asks for a person's
judgement. The ticket's own closing note stands — a real dual-client pass remains the user's final unified
acceptance, which this offline batch neither performs nor substitutes for.

## Limits

- **No client was started.** No rendering, frame, live log or probe was observed; every row is decided by
  this run's test output against the tree that produced the deployed artifact.
- **The historical suite numbers were not re-measured.** The ticket states "2451 runtime tests + 16
  normative gates passed" and its self-check states "81 focused tests"; this run measured 626
  normative-gate cases and 5072 behavioural cases, both 0 failed / 0 skipped (`gates.log`, `full.log`). The
  2451 and 81 figures are the writing cycle's measurements.
- **The ticket's self-check bullet is a process record, not a row here.** "Three fresh-subagent passes"
  were the writing cycle's reviews; this batch did not re-run them. The rows read the durable outcome —
  the cases the findings produced are green in this run.
- **Row 7 is judged through the swing-hold state machine.** The test names the hold's window behaviour, not
  the traffic snapshot it avoids building; that the idle path no longer builds a full snapshot is read
  from the ticket and the self-check, not re-measured by a case of its own.
