# Acceptance record — Global adaptive report-rate flow control — Stage 4: remaining high-frequency domain streams

- Ticket: `global-adaptive-report-rate-stage-4-high-frequency-domains` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket states its landed work as prose design decisions, a stream-catalog table and a verification
list, so this run writes one row per verifiable claim and judges each against this run's own suite output
and the committed tree.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The five remaining frequent streams opt in with the stated base cadences — world item movement 10 Hz, world item snapshot 5 s, fluid diff 10 Hz, fluid full-viewport 1 s, trader fallback 5 s — each as `LatestWins` | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamCatalogTests.Stage4Streams_UseExpectedBaseCadences` — `Passed`; the catalog source read in this run carries the five entries (`WorldItemMoveStream`, `WorldItemSnapshotStream`, `FluidRegionDiffStream`, `FluidRegionFullStream`, `TraderStateStream`), each with `AdaptiveStreamDeliveryMode.LatestWins` |
| 2 | `AdaptiveStreamProfile` gains an interval-based mode (`BaseIntervalMs`, optional `MaxIntervalMs`) in addition to the Hz mode, and the rate policy applies the same pressure/priority factor to the base interval, so pressure lengthens the interval instead of being clamped by a 1 Hz floor | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.IntervalStream_Pressure_IncreasesInterval(pressure: High)` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.IntervalStream_MaxInterval_CapsPressureGrowth` — `Passed` (with `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.IntervalStream_Optimal_KeepsConfiguredBaseInterval` green in the same run) |
| 3 | `AdaptiveStreamRateService` exposes `GetEffectiveIntervalMs` and delegates `GetSendIntervalMs` through it; a broadcast interval query picks the most constrained (longest) peer interval, and an empty peer set returns the optimal base | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.IntervalBroadcast_UsesLongestWorstPeerInterval` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.IntervalBroadcast_EmptyPeers_ReturnsOptimalBase` — `Passed` (with `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.IntervalStreams_UseProfileBaseIntervalInOptimal` green in the same run) |
| 4 | Fluid diff and full-viewport frames travel as the same fluid-region payload, the full-viewport flag is a sender-only non-wire hint, and `PacketSender` classifies each frame into separate observation-only payload types — including the full-size diff edge case — so the two cadences cannot cross-pollute each other's estimates | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.PacketSenderFluidRegionClassificationTests.FluidRegionMessages_ClassifyDiffAndFullSeparately` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.Stage4Streams_MapToWireObservations` — `Passed` |
| 5 | The trader-state periodic fallback stays reliable and is only cadence-adapted, so it cannot overtake or be overtaken by the immediate action broadcast on their shared message | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.TradeStateSync_ConsumesAdaptiveRates` — `Passed` (the fallback asks the shared service for its cadence and nothing in the adaptive path drops or reorders it); `CasualtiesUnknownOnline.Tests.Session.SyncCadenceDecisionTests.TraderFallback_WorstCaseDivergence_IsCappedAtFifteenSeconds` — `Passed` |
| 6 | The Game Adapter domain owners (`ItemPositionAuthority`, `FluidSimulationAuthority`, `TradeStateSync`) query the shared `AdaptiveStreamRateService` for their next send cadence instead of hard-coded constants, and clear their session state when a session ends | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.ItemPositionAuthority_ConsumesAdaptiveRates` — `Passed` (with `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.FluidSimulationAuthority_ConsumesAdaptiveRates`, `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.TradeStateSync_ConsumesAdaptiveRates` and `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.GameAdapter_ConstructorAcceptsAdaptiveRates` green in the same run); `CasualtiesUnknownOnline.Tests.Patching.AdaptiveStage4ContractTests.FluidWorldSync_ResetsKernelAndAuthority` — `Passed` (the session-reset half) |
| 7 | The cycle's self-check record exists in the tree as a durable artifact | `machine` | **pass** | `docs/evidence/selfchecks/protocol/global-adaptive-report-rate-stage-4-high-frequency-domains-selfcheck.md`, read in this run — "# Global adaptive report rate Stage 4 — remaining high-frequency domain streams self-check" |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs or of the committed tree, and no
row asks for a person's judgement. The ticket's own closing note stands — a real dual-client pass remains
the user's final unified acceptance, which this offline batch neither performs nor substitutes for.

## Limits

- **No client was started.** No rendering, frame, live log or probe was observed; every row is decided by
  this run's test output, by the deployed artifact's identity, or by inspection of the committed tree.
- **The historical suite numbers were not re-measured.** The ticket states "2481 runtime tests + 16
  normative gates passed"; this run measured 626 normative-gate cases and 5072 behavioural cases, both
  0 failed / 0 skipped (`gates.log`, `full.log`). The 2481 figure is the writing cycle's measurement.
- **The ticket's self-check bullet is a process record, not a row here.** "Two fresh-context passes" were
  the writing cycle's reviews and this batch did not re-run them; the rows read the durable outcome.
- **Two cells of the ticket's stream table are stale, and this run's evidence follows the tree instead.**
  The table gives `MaxIntervalMs` 30000 ms for `WorldItemSnapshotStream` and for `TraderStateStream`; the
  committed catalog caps them at 10 s and 15 s, and this run's own cases follow the committed values —
  `CasualtiesUnknownOnline.Tests.Session.SyncCadenceDecisionTests.ItemKeyframe_WorstCaseDivergence_IsCappedAtTenSeconds` and
  `CasualtiesUnknownOnline.Tests.Session.SyncCadenceDecisionTests.TraderFallback_WorstCaseDivergence_IsCappedAtFifteenSeconds`, both `Passed`.
  Row 1 therefore passes on the base cadences and the interval mode the ticket claims, not on those two
  numbers; the ticket prose should be re-pointed when the orchestrator moves it.
- **The cycle's deploy claim was not re-measured as stated.** "SHA-256 hashes of all six CUO DLLs match the
  build output" is that cycle's comparison; this run's own identity check is the header's deployed artifact
  line (`verify-deploy.log`, exit 0).
