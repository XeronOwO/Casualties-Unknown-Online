# Acceptance record — Checkpoint chunks are not validated against the current run epoch

- Ticket: `checkpoint-run-epoch-validation` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A stale set from the previous run arrives after the live run is established → refused with a log; live state and identity untouched; the live run's stream still applies. | machine | pass | `KernelProtocolServiceTests.Guest_RefusesCheckpointSetFromAnotherRun` passed: the old run's item never lands, the live item stays, the epoch does not move back and the live stream is still applied; the log half is the refusal branch of `src/CasualtiesUnknownOnline.Application/Kernel/GuestCheckpointReceiver.cs`, which logs a warning once per offending run before anything is buffered |
| 2 | Legitimate checkpoint with no announcement yet (fresh join / rejoin in a new session) → accepted; the guest adopts its epoch, and the next set is compared against what that one established. | machine | pass | `KernelProtocolServiceTests.Guest_AdoptsTheIdentityFromTheFirstRestoredSet`, `KernelProtocolServiceTests.Host_SendsCheckpoint_AndGuestRestoresItemState` and `KernelProtocolServiceTests.Guest_AcceptsStateStreamAfterARealCheckpointRestore` all passed |
| 3 | Reconnect while in world → checkpoint accepted; no epoch regression. | machine | pass | `KernelProtocolServiceTests.Disconnect_ThenReconnectWithCheckpoint_RestoresGuest` passed |
| 4 | A mixed-epoch set handed to the assembler → refused. | machine | pass | `KernelWireMapperTests.CheckpointAssemble_ChunksDisagreeingOnEpoch_Throws` passed |
| 5 | The announced run is what decides, and a host run reset mid-connection → the instruction's identity outranks the set's own stamp and this side's epoch; the previous run's set is refused, the announced one accepted. | machine | pass | `KernelProtocolServiceTests.Guest_RefusesASetFromARunTheWorldJoinDidNotAnnounce` and `KernelProtocolServiceTests.Guest_ServesTheRunTheWorldJoinAnnounced` passed, together with row 1's case |
| 6 | Partial chunk set → buffered, nothing restored. | machine | pass | `KernelProtocolServiceTests.Guest_SupersededSetOfTheSameRunDoesNotBlockTheLiveSet` passed (the kernel revision is unchanged while a partial set is buffered); its sibling `KernelProtocolServiceTests.Guest_StaleChunkDoesNotBlockTheLiveCheckpointSet` passed too |
| 7 | Malformed frame: header stamp ≠ payload stamp → refused. | machine | pass | `KernelProtocolServiceTests.Guest_RefusesCheckpointChunkWhoseHeaderDisagreesWithItsPayload` passed; the same branch logs the mismatch in `src/CasualtiesUnknownOnline.Application/Kernel/GuestCheckpointReceiver.cs` |
| 8 | The wire field itself is pinned. | machine | pass | `NetPacketTests.WorldJoin_RunEpoch_RoundTrips` passed in the main suite |

## Residuals for the user
None.

## Limits
No client was started: every row is a receive-seam test outcome, and the log expectations in rows 1 and 7 are decided by the refusal branches' own log statements in the receiver source rather than by a log-capturing test. The ticket's declared limits hold unchanged: the announcement is deliberately not written into the guest's kernel epoch, a stale set arriving before the first instruction of a connection is accepted by definition, the repair group's trigger path is not what these cases pin, and the index-keyed-buffer defect is demonstrated at the receive seam rather than reproduced from a live session.
