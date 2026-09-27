# Acceptance record — Protocol frame envelope validation

- Ticket: `protocol-frame-validation` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This ticket carries no acceptance section, so its `## Landed` section is enumerated claim by claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A unified `ProtocolFrameValidator` sits in front of `KernelProtocolService.HandleFrame`. | machine | pass | `src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs` holds `TryValidate(ProtocolFrame?, ulong? expectedSender, out string error)` and `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs` builds its envelope header with `ProtocolVersion = ProtocolConstants.EnvelopeVersion`; `ProtocolFrameValidatorTests` (22 cases, all `Passed` in the main suite) is the unit suite the ticket names |
| 2 | It validates exactly one envelope plus a kind match. | machine | pass | the validator rejects a missing/multiple envelope and an undefined or mismatched kind; `ProtocolFrameValidatorTests.MissingEnvelope_Fail`, `MultipleEnvelopes_Fail`, `UnknownFrameKind_Fail` and `KindEnvelopeMismatch_Fail` are `Passed`, and `KernelProtocolServiceTests.Host_DropsMalformedFrameWithMultipleEnvelopes` is `Passed` |
| 3 | It validates the payload discriminator. | machine | pass | `ValidatePayloadDiscriminator` switches on the envelope kind and its payload; `ProtocolFrameValidatorTests.KindEnvelopeMismatch_Fail`, `CommandPayloadKindMismatch_Fail`, `CommandWithNonCommandPayload_Fail`, `CommittedBatchWithWrongPayload_Fail`, `CheckpointWithWrongPayload_Fail` and `StateStreamWithWrongPayload_Fail` are `Passed` |
| 4 | It validates transport sender consistency. | machine | pass | the validator refuses a header sender that does not match the transport sender (`expectedSender` check); `ProtocolFrameValidatorTests.ForgedSenderId_Fail` and `KernelProtocolServiceTests.Host_DropsCommandWithForgedSender` are `Passed` |
| 5 | It validates checkpoint metadata. | machine | pass | the checkpoint branches reject a bad chunk index, chunk count and oversized chunk; `ProtocolFrameValidatorTests.CheckpointInvalidChunkIndex_Fail`, `CheckpointInvalidChunkCount_Fail`, `CheckpointOversizedChunk_Fail` and `KernelProtocolServiceTests.Guest_RefusesCheckpointChunkWhoseHeaderDisagreesWithItsPayload` are `Passed` |
| 6 | It validates collection bounds. | machine | pass | `ValidateCollectionBounds` enforces `ProtocolConstants.MaxStateStreamCollectionSize` on the state-stream field, item-move, item-state, player-state and enemy-state counts and on committed-batch events; `ProtocolFrameValidatorTests.StateStreamOversizedCollection_Fail` and `CommittedBatchOversizedEvents_Fail` are `Passed` |
| 7 | Unknown presentation payloads remain non-fatal. | machine | pass | the validator returns valid for a payload type at or above `ProtocolConstants.PresentationPayloadStart`; `ProtocolFrameValidatorTests.PresentationPayload_IsNonFatal` and `UnknownCriticalPayload_Fail` are `Passed` (the second pins that the non-fatal carve-out does not extend to unknown critical payloads) |
| 8 | Malformed/forged-frame integration cases in `KernelProtocolServiceTests` and a green full suite. | machine | pass | `KernelProtocolServiceTests` integration cases are `Passed` — `Host_DropsMalformedFrameWithMultipleEnvelopes`, `Host_DropsCommandWithForgedSender`, `Guest_DropsUnsupportedEnvelopeVersion`, `Guest_RefusesCheckpointChunkWhoseHeaderDisagreesWithItsPayload`, `Guest_DropsBatchWithMismatchedPayload`; the main suite ran 4482/4482 with 0 not executed and the gate suite 288/288 |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe. Rows 1–7 are decided by `ProtocolFrameValidatorTests` plus direct inspection of `ProtocolFrameValidator.cs`; row 8 by the named `KernelProtocolServiceTests` cases and the batch's suite totals. The ticket says the validator suite has 22 cases and `trx-outcomes.txt` indexes exactly 22 `ProtocolFrameValidatorTests` outcomes, all `Passed` — the earlier count is confirmed, not assumed. Host/guest behaviour is judged by the in-process protocol tests, not by a two-client session. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
