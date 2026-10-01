# Acceptance record — Snapshot size reduction

- Ticket: `snapshot-size-reduction` — verdict: moved to `done/`
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47
  (`KernelWireMapperTests` 9/9, `NetworkTrafficBaselineTests` 4/4); the run-local checkpoint-size
  probe; full suite with build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the run-local probe, the repository's own history
  (`git show 4e16bac1`, `git show 7bcc982a`); no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` —
  `probe-checkpoint-sizes.txt`, `focused-20261001-t.log`, `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Checkpoint snapshots use a checkpoint-local item-definition string table: `WireCheckpoint.ItemDefinitionTable` (chunk 0), `WireItemIdentity.DefinitionIndex` (0 = direct-string fallback), built and expanded by `WireCheckpointAssembler.Split` / `Assemble` | machine | pass | source read of the three members and the assembler this run; `KernelWireMapperTests` 9/9 `Passed`, including the round trip that preserves the identities, the direct-string case for all-unique checkpoints, and the out-of-table index throw |
| 2 | No change to the item kernel model, the command/event wire, state-stream payloads, chunk batching or reliability semantics; all-unique checkpoints keep the direct-string form | machine | pass | `KernelWireMapperTests.CheckpointSplit_UniqueDefinitionIdsKeepDirectStrings` `Passed`; the assembler only rewrites the checkpoint-local chunk shape; the full suite stays green |
| 3 | The wire shape change bumps the versions in the same change: `ProtocolVersion.Current` 4 → 5 and `CheckpointSchemaVersion` 1 → 2; the handshake rejects a mixed session before any checkpoint | machine | pass | `git show 4e16bac1` (the string table) bumps `CheckpointSchemaVersion` 1 → 2; `git show 7bcc982a` (the same cycle's handshake bump) sets `ProtocolVersion.Current` 4 → 5; today the tree reads `CheckpointSchemaVersion = 2` (and the protocol constant stands at 44 after later legitimate bumps); `HandshakeHandler` / `HandshakeAckHandler` refuse a peer whose version differs |
| 4 | The measured regression: the repeated 600-item checkpoint drops from 25,732 to 23,939 bytes (−1,793, ≈7%); the size-budget check was red before and green after | machine + run-local probe | pass | the probe reproduced both encodings of the same 600-item checkpoint: compact 23,939 bytes, direct-string 25,732 bytes (delta 1,793; the direct form exceeds the 24,000-byte budget the test asserts, the compact form passes it). `NetworkTrafficBaselineTests.CheckpointSnapshotSize_RepeatedDefinitionIds_OverheadBudget` and `CheckpointBaseline_RecordsChunkCountSizeAndRestoreTime` `Passed` |

## Residuals for the user

None.

## Limits

- The probe is run-local: it was deleted before the final ladder, its output file is the artifact cited
  above, and no test or product file in the delivered tree depends on it.
- The ticket's selfcheck keeps the cycle's own numbers (`docs/evidence/selfchecks/protocol/
  checkpoint-string-table-selfcheck.md`); this run re-derived the two byte figures and the budget
  behaviour rather than quoting them.
- No runtime behaviour is observed; Steam's own framing of the checkpoint frames is outside this batch,
  as the ticket's own record says.
