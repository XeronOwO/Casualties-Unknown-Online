# Acceptance record — Application layer: first slice (kernel command gateway and the kernel replication move)

- Ticket: `application-layer-first-slice` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The direction gate fails on a synthetic GameState → Application reference and is green on the tree. | machine | pass | `ProjectDirectionGateTests.GameStateReferencingUpward_IsRefused` (the synthetic upward reference is refused) and `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` (the tree half) both passed in the gate suite 288/288; the neighbouring controls `ProjectDirectionGateTests.RuntimeReachingGameStateWithoutTheLayer_IsRefused`, `ProjectDirectionGateTests.AbstractionsReachingDown_IsRefused` and `ProjectDirectionGateTests.ApplicationReferencingItsLowerLayers_IsAccepted` passed too |
| 2 | A command that is refused today at an entry point is refused by the gateway with the same RejectionReason, pinned by a test that names the scenario; the ORDER of refusals does not change (an eligibility refusal must not start masking a domain refusal). | machine | pass | `KernelCommandGatewayTests.HostOnlyCommandFromAMember_IsRefusedAndAnswered`, `KernelCommandGatewayTests.PresentationOnlyCommandFromAMember_IsRefusedAndAnswered`, `KernelCommandGatewayTests.CommandWhoseActorIsNotTheSender_IsRefusedAndAnswered` and `KernelCommandGatewayTests.Refusal_IsAuditedOnceWithTheCommandActorSenderAndReason` passed (12/12 in the class); the not-masking half is `CommandAdmissionIntegrationTests.NonOwnerDrop_IsStillRefusedByTheKernelWithItsOwnReason` and `CommandAdmissionIntegrationTests.DestroyOfAnItemNobodyReported_IsRefusedByTheCreationBeforeOperationInvariant`, both passed; the refusal reason itself is RejectionReason.NotAuthorized in `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` |
| 3 | Stage 3 is a pure move: the full suite is green and no behaviour diff is claimed. | machine | pass | main suite 4482/4482 and gate suite 288/288 passed with 0 not executed; the move's boundary is pinned by `KernelReplicationLayerBoundaryTests` 17 passed, including `KernelReplicationLayerBoundaryTests.ApplicationAssembly_NeverReferencesTheRuntimeOrTheLayersAboveIt` and the per-type `KernelReplicationLayerBoundaryTests.KernelReplicationType_LivesInTheApplicationAssembly` cases for the moved kernel replication types |

## Residuals for the user
None.

## Limits
No client, no rendering and no deployed artifact were exercised. Rows 1 and 2 are decided by the gate and suite outcomes above; row 3 is decided by those outcomes plus the boundary class, and "no behaviour diff is claimed" is read from the ticket's own stage-3 text. The layer's real behaviour under a live session (transport, hosts and guests on a running game) was not observed and is not claimed by this batch. The ticket's own cycle numbers (3 773 tests, 93 gates, focused families) are not re-used as evidence here.
