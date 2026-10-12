# Acceptance record — Global adaptive report-rate flow control — Stage 3: cumulative stream coalescing

- Ticket: `global-adaptive-report-rate-stage-3-cumulative-streams` — verdict: **moved to `done/`**
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
| 1 | Injection frame-level deltas are coalesced into fewer, larger, still-reliable `MedicalOperationUpdate` frames at the adaptive cadence, and the terminal end request still reconciles the exact delivered amount | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.MedicalOperationSessionServiceTests.Guest_InjectionBurst_CoalescesIntoOneReliableFrameAfterInterval` — `Passed` (a burst becomes one frame); `CasualtiesUnknownOnline.Tests.Session.MedicalOperationSessionServiceTests.Guest_InjectionStartUpdateEnd_CarriesEachDoseOnceAndEmitsSingleEnd` — `Passed` (each dose arrives exactly once, so the committed amount is unchanged) |
| 2 | Shrapnel ordinary held-piece positions coalesce to the latest position per piece at the adaptive cadence, ownership and release transitions stay immediate, and the buffered final position is flushed reliably before release/end/cancel | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.ShrapnelPositionStreamTests.Guest_OrdinaryShrapnelPositions_CoalesceLatestAfterInterval` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.ShrapnelPositionStreamTests.Guest_Release_PreservesLastAuthoritativePosition` — `Passed` (with `CasualtiesUnknownOnline.Tests.Session.ShrapnelPositionStreamTests.Guest_End_FlushesBufferedPositionBeforeTerminal`, `CasualtiesUnknownOnline.Tests.Session.ShrapnelPositionStreamTests.Guest_Cancel_FlushesBufferedPositionBeforeCancel` and `CasualtiesUnknownOnline.Tests.Session.ShrapnelPositionStreamTests.Guest_WireShrapnelUpdate_ReachesHostTransport` green in the same run) |
| 3 | `AdaptiveStreamProfile` gains an optional `BaseHz`, so medical progress streams keep their frame-level cadence in `Optimal` while still lowering under pressure, without repurposing the global 20 Hz baseline | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamCatalogTests.MedicalProgressStreams_UseFrameLevelBaseHz` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamRateServiceTests.MedicalStreams_UseProfileBaseHzInOptimal` — `Passed` |
| 4 | A shared `MedicalOperationIdAllocator` feeds the injection, shrapnel and other-action domains, so same-numbered operations from different medical sub-domains cannot confuse terminal routing | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.MedicalOperationIdAllocatorTests.Allocator_ReturnsMonotonicUniqueIds` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.ShrapnelOwnershipSessionTests.TwoGuests_JoinSameSharedShrapnelSession_ShareOneOperationId` — `Passed` (one allocator's id is shared across the joins in the same operation) |
| 5 | The catalog carries the two new streams with the stated modes — `MedicalInjectionReport` as `Cumulative` on a reliable transport, `ShrapnelPositionReport` as `LatestWins` on an unreliable one — and the policy now adapts `Cumulative` | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveRatePolicyTests.Cumulative_IsAdaptedInStage3` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.AdaptiveStreamWireMapperTests.MedicalStreams_MapToDedicatedPayloadTypes` — `Passed` (the new streams are catalogued and mapped; the catalog source read in this run carries both entries with `BaseHz: 60`) |
| 6 | `PacketSender` classifies injection, shrapnel ordinary and other medical updates into separate traffic-observation payload types, so the sub-streams cannot cross-pollute each other's bandwidth estimates | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.AdaptiveSync.PacketSenderMedicalUpdateClassificationTests.MedicalUpdateMessages_ClassifyBySubStream` — `Passed` |
| 7 | The cycle's self-check record exists in the tree as a durable artifact | `machine` | **pass** | `docs/evidence/selfchecks/protocol/global-adaptive-report-rate-stage-3-cumulative-streams-selfcheck.md`, read in this run — "# Global adaptive report rate Stage 3 — cumulative stream coalescing self-check" |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs or of the committed tree, and no
row asks for a person's judgement. The ticket's own closing note stands — a real dual-client pass remains
the user's final unified acceptance, which this offline batch neither performs nor substitutes for.

## Limits

- **No client was started.** No rendering, frame, live log or probe was observed; every row is decided by
  this run's test output, by the deployed artifact's identity, or by inspection of the committed tree.
- **The historical suite numbers were not re-measured.** The ticket states "2462 runtime tests + 16
  normative gates passed"; this run measured 626 normative-gate cases and 5072 behavioural cases, both
  0 failed / 0 skipped (`gates.log`, `full.log`). The 2462 figure is the writing cycle's measurement.
- **The ticket's self-check bullet is a process record, not a row here.** "Three fresh-subagent passes"
  were the writing cycle's reviews and this batch did not re-run them; what the rows read is the durable
  outcome — the cases those findings produced are green in this run.
- **The cycle's deploy claim was not re-measured as stated.** "SHA-256 hashes of all six CUO DLLs match the
  build output" is that cycle's comparison; this run's own identity check is the header's deployed artifact
  line (`verify-deploy.log`, exit 0).
- **Row 7 is judged as a durable artifact.** The self-check file exists and is the record the ticket names;
  its findings are not re-derived by this run.
