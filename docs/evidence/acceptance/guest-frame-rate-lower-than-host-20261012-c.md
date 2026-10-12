# Acceptance record — Guest frame rate lower than host with frame drops

- Ticket: `guest-frame-rate-lower-than-host` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket's "Completed in this cycle" section is prose, so this run writes one row per landed claim and
judges each against this run's own suite and gate output and the committed tree.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Opt-in whole-frame latency telemetry (`[Latency] Frame`) exists alongside the per-domain summaries, with a configurable slow-frame / frame-drop threshold | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Diagnostics.LatencyInstrumentationTests.Enabled_RecordFrameAggregatesCallsAndSlowCount` — `Passed` (one whole-frame sample carries calls, total/avg/max and the slow count); `CasualtiesUnknownOnline.Tests.Diagnostics.LatencyInstrumentationTests.Disabled_RecordFrameDoesNotCollect` — `Passed` (the opt-in path costs nothing when off); the committed self-check names the key: `docs/evidence/selfchecks/tooling/guest-frame-rate-baseline-selfcheck.md` — "`LatencyOptions.SlowFrameThresholdMs` is configurable through `Diagnostics:SlowFrameThresholdMs`" |
| 2 | Avoidable per-frame allocations were removed: the remote-player view is a cached list rebuilt only on entity-table mutations and read by index in the hot consumers, and the guest item-follow pump no longer takes a key snapshot every frame | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.StateStreamTests.PlayerLeave_RemovesRemoteBuffer` — `Passed` (the cached remote-player view's lifecycle: gone after leave, with `CasualtiesUnknownOnline.Tests.Session.StateStreamTests.GuestReport_ReachesHostAndUpdatesSyncedMember` green in the same run for the populated case); `docs/evidence/selfchecks/tooling/guest-frame-rate-baseline-selfcheck.md` — "`ItemPositionFollow.Update` no longer calls `_follow.Keys.ToList()` every frame" |
| 3 | The cycle's self-check record exists in the tree as a durable artifact | `machine` | **pass** | `docs/evidence/selfchecks/tooling/guest-frame-rate-baseline-selfcheck.md`, read in this run — "# Guest frame rate lower than host — baseline/overhead isolation self-check" |
| 4 | The cycle's verification holds up as a durable outcome: the full suite is green and the architecture, event-replay and entity-event-dispatch gates pass | `machine` | **pass** | This run's own per-case index, `test-outcomes.txt`, holds 5681 cases and every one is `Passed` (no failed, no skipped case); the three gates the ticket names are green in this run — `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` — `Passed`, `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.RepositoryGateTests.EventReplayMatrix_Completeness` — `Passed`, `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.RepositoryGateTests.EntityEventDispatch_AllKindsCoveredInEveryTable` — `Passed` |

## Residuals for the user

The ticket's own remaining evidence is a person's pass, and it stays theirs: with the telemetry enabled,
compare a host and a guest log (frame calls, average, max, slow count; then the per-domain rows) and judge
whether the guest's frame rate is still visibly lower. The self-check says exactly this — it deliberately
"does not claim a measured FPS number from a manual dual-client acceptance". This offline batch reads no
frame and no log, so it neither performs nor substitutes for that comparison.

## Limits

- **No client was started.** The ticket's symptom — a guest frame rate lower than the host's, with visible
  frame drops — is a runtime observation this run cannot make and does not claim. What is judged here is
  the landed work: the telemetry surface and the allocation removals, as this run's tests, gates and the
  committed self-check show them.
- **The historical suite count was not re-measured.** The ticket states "full test suite 2283 passed /
  0 failed"; this run measured 5072 behavioural cases and 626 normative-gate cases, both 0 failed /
  0 skipped (`full.log`, `gates.log`). The 2283 figure is the writing cycle's measurement.
- **No frame time was measured anywhere in this run.** No probe, no capture and no live log line; the
  telemetry row is grounded on its cases and on the committed emitter description, not on a collected
  sample.
