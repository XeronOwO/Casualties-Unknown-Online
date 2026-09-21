# Application layer: first slice (command admission seam)

Date: 2026-09-21
Scope: ticket `docs/backlog/review/application-layer-first-slice.md` stages 1-3 — the
`CasualtiesUnknownOnline.Application` project, the declared project-direction gate, the kernel
command admission seam, and the kernel-replication move (stage 3: the ports plus the eight types that
could move — see the stage 3 section below). Decisions 210 (the seam) and 211 (the move) record the
rulings. Full-tier independent review of stages 1-2: `%TEMP%\cuo-review-application-layer.md`
(0 blockers / 3 majors / 7 minors / 4 nits, all majors fixed in that cycle); the stage 3 review report
is named in the cycle handoff.

## What landed

- **The layer exists and is the only way down.** `src/CasualtiesUnknownOnline.Application/` is in
  `CasualtiesUnknownOnline.slnx`, references `GameState`, `Protocol` and
  `Microsoft.Extensions.Logging.Abstractions` and nothing else; `CasualtiesUnknownOnline.Runtime`
  declares no direct `GameState` reference any more and reaches the kernel through the layer
  (transitive compile reference — the layer is a DECLARATION boundary, not an encapsulation one: 121
  `using CasualtiesUnknownOnline.GameState…` directives remain in the Runtime). The interim-seam
  comment the Runtime csproj carried since Phase A is replaced by the direction it promised.
- **The direction is data with a gate.** `ProjectDirectionPolicy.AllowedReferences` /
  `RequiredReferences` / `ConsumerProjects` hold the table; `ProjectDirectionGateTests` loads the
  graph from the solution (every listed project; `ProjectReference` and raw CUO `Reference` items),
  applies a census floor and a CLASSIFICATION census (every solution project must be declared or
  listed as a consumer), refuses an upward reference, a project above the layer that references
  `GameState`, and a Runtime that stops referencing the layer, and keeps the eight synthetic cases
  that prove the checker still refuses what it must.
- **An admission seam for a member's mapped submission.** `KernelCommandGateway` (Application layer)
  applies, in a fixed order: the command's actor must be the submitting member; the declared
  authority kind must be one a member may author (`HostOnly` and `PresentationOnly` are refused —
  the enum's own meaning, not a new rule); a destroy report must name a world item or the sender's
  own carried item. Every refusal is audited with one uniform line naming the command, the item it
  names, the actor, the sender and the reason; an admitted command is not audited.
- **What is deliberately outside it.** The handler's two protocol heals (a container-sync report and
  an update for an unknown carried id — they materialize the reporter's own carried parent, i.e. the
  host's own write, and the second one stamps its own authority kind) and a guest's range request
  (answered in `KernelProtocolService`, never a command). Those paths are pre-existing and unchanged;
  the seam covers the mapped member-authored command.
- **The moved check keeps its silence.** The verdict is three-valued: `Admitted`, `Refused` (the
  sender is answered) and `Ignored` (no answer — the destroy shape that was always dropped in
  silence). The seam sits at the POSITION the old `CanDestroy` check held, after the protocol heals
  and the creation-before-operation invariant, so an eligibility refusal cannot mask a domain one.
- **A namespace collision surfaced and is fixed.** `Plugin.cs` (namespace `CasualtiesUnknownOnline`)
  resolves `Application` to the new CUO namespace, not to `UnityEngine.Application`; its five Unity
  call sites now go through `using UnityApplication = UnityEngine.Application;`. The pitfall is
  recorded in `docs/development/agent-reference.md` Known Pitfalls, together with the per-project
  `IsExternalInit` shim a new net48 project needs for records.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Project direction | Runtime -> Application -> GameState; no direct `GameState` reference from Runtime, GameAdapter or Plugin; Abstractions/GameState/Protocol reference nothing | `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` (green on the tree, census floor over 13 projects / 10 references, classification census) |
| Direction gate itself | Declared table + explicit consumer list + pure checker | `ProjectDirectionGateTests.GameStateReferencingUpward_IsRefused`, `.RuntimeReachingGameStateWithoutTheLayer_IsRefused`, `.ApplicationReferencingUpward_IsRefused`, `.ApplicationReferencingItsLowerLayers_IsAccepted`, `.PluginReachingGameStateDirectly_IsRefused`, `.ConsumersAreNotConstrained`, `.UndeclaredProject_IsRefusedInsteadOfSilentlyExempt`, `.AbstractionsReachingDown_IsRefused`, plus the control run below |
| Actor-to-sender binding | Now local to the seam instead of three files away (`ProtocolFrameValidator` still guarantees it) | `KernelCommandGatewayTests.CommandWhoseActorIsNotTheSender_IsRefusedAndAnswered` |
| Declared authority policy | `HostOnly` / `PresentationOnly` refused for a member submission; `OwnerPredictedHostValidated` / `TriggerObservedHostCommitted` admitted | `KernelCommandGatewayTests.HostOnlyCommandFromAMember_IsRefusedAndAnswered`, `.PresentationOnlyCommandFromAMember_IsRefusedAndAnswered`, `.OwnerPredictedCommandFromItsOwnActor_IsAdmitted`, `.ObservedCommandFromAMember_IsAdmitted` |
| Item destroy eligibility | Moved verbatim out of the handler's former `CanDestroy` (deleted with this change); silent verdict preserved | `KernelCommandGatewayTests.DestroyOfAnotherMembersCarriedItem_IsIgnoredWithoutAnAnswer`, `.DestroyOfTheSendersOwnCarriedItem_IsAdmitted`, `.DestroyOfAWorldItem_IsAdmitted`; end-to-end `ItemDestroyAuthorityTests` + `CommandAdmissionIntegrationTests.NonOwnerDestroy_ProducesNoAnswerAtAll` |
| Refusal order (no masking) | The heals and the creation-before-operation invariant still refuse first; a domain refusal still reaches the sender | `CommandAdmissionIntegrationTests.DestroyOfAnItemNobodyReported_IsRefusedByTheCreationBeforeOperationInvariant`, `.NonOwnerDrop_IsStillRefusedByTheKernelWithItsOwnReason`; the seam's own unjudged-id branch is defensive, not the production path (`KernelCommandGatewayTests.DestroyOfAnIdThisHostNeverJudged_IsNotTheSeamsToAnswer`) |
| Uniform audit line | One structured line per refusal (command, item id when the command names one, actor, sender, reason, answered/not) | `KernelCommandGatewayTests.Refusal_IsAuditedOnceWithTheCommandActorSenderAndReason`, `.IgnoredDestroyReport_IsAuditedWithTheItemIdItNamed`, `.Admission_IsNotAudited` |
| Composition | `IKernelItemFacts` port in the Application layer, implemented by `ItemKernelAuthority`; gateway registered in `CuoBootstrap` | `CuoBootstrap` registrations; the integration tests above run the production composition root |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0.
- `dotnet test CasualtiesUnknownOnline.slnx` (build included) — gates 93/93, tests 3 762/3 762 at
  stages 1-2; the post-move run is recorded in the stage 3 section below (3 773, +11 for the
  layer-boundary class).
- Focused, `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~KernelCommandGatewayTests|FullyQualifiedName~ProjectDirectionGateTests|FullyQualifiedName~CommandAdmissionIntegrationTests|FullyQualifiedName~ItemDestroyAuthorityTests"`
  — 9/9 in the gates project (1 tree + 8 synthetic) and 17/17 in the tests project (12 gateway cases,
  3 admission/order cases, 2 pre-existing destroy-authority cases); 26 together.
- Feedback-tier census re-measured for `docs/evidence/test-parallelization.md` §9.1: 260 classes /
  1 694 cases tagged (the move added no tagged class) and 2 079 cases untagged (fast subset 21 s
  wall, tagged 33 s, single `--no-build` run each); the move's own delta is 11 untagged cases
  (`KernelReplicationLayerBoundaryTests`), so the pre-move figure was 2 068 / 27 s / 41 s.
- **Gate control run** (after the review fixes, rerun): with a direct `GameState` reference
  temporarily injected into `Runtime.csproj`,
  `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` fails with
  `CasualtiesUnknownOnline.Runtime references CasualtiesUnknownOnline.GameState, which its declared
  layer does not allow` (1 failed / 8 passed), and the file was restored byte-identically
  (SHA-256 `b50e4d78…7bafd` before and after). The tree half therefore fails on the violation it
  exists for, not only in the synthetic cases.

## Stage 3 — the kernel replication move (same cycle)

### What landed

- **Eight types moved into `CasualtiesUnknownOnline.Application/Kernel/`**: `IKernelProtocolControl`,
  `RefusedItemCreations`, `KernelDomainWireMapper`, `KernelStateStreamService`,
  `WireCheckpointAssembler`, `GuestCheckpointReceiver`, `KernelProtocolService` and
  `KernelProtocolCommandHandler`. The three that needed no port (`IKernelProtocolControl`, `RefusedItemCreations` and
  `KernelDomainWireMapper`) moved unchanged; the rest moved onto the ports below.
- **Seven declared ports, one per capability**, each answered by the Runtime service that owns it.
  No port mirrors a Runtime service: the session port carries the role as two sides and the
  handshaken peer ids (never a presence record), the transport port carries protocol frames (never
  the transport's own message vocabulary), and the kernel authority arrives as four capabilities
  beside the existing read-only `IKernelItemFacts`.
- **Adapters bind to what was already there**: the four Runtime services gained the interface on
  their declaration (a public declaration change) — eleven of the port members are explicit
  forwarders added with this change and the other eleven bind to members that were already public at
  HEAD, so no new member is reachable by name. The only new Runtime types are the feature's
  registration block and the codec adapter.
- **The registrations moved out of `CuoBootstrap`** into `KernelReplicationComposition`, unchanged and
  in the same order (the `ICuoService` update order does not move); the file goes 600 -> 586 lines,
  which is the same reason the content-vocabulary block has its own file.
- **`WireCheckpointAssembler.Split`/`Assemble` take the codec as a parameter**, so the checkpoint
  helpers stay pure functions; the tests round-trip through the production adapter
  (`TestKernelCodec`), not a fake.

### Mechanism inventory (stage 3)

| Mechanism | Change | Evidence |
|---|---|---|
| Layer boundary | The moved types are declared in the Application assembly; the Application assembly references no Runtime/GameAdapter/Plugin/game assembly; the Runtime still references the layer | `KernelReplicationLayerBoundaryTests.KernelReplicationType_LivesInTheApplicationAssembly` (8 cases), `.ApplicationAssembly_NeverReferencesTheRuntimeOrTheLayersAboveIt`, `.RuntimeReachesTheMovedSurfaceThroughTheApplicationAssembly` |
| Recorded exceptions | `KernelWireMapper`, `KernelBatchItemProjection` and `KernelEnvelopeHandler` stay in the Runtime, named with their blocker | `KernelReplicationLayerBoundaryTests.LegacyCoupledTypes_StayInTheRuntimeForTheirOwnSlice`; `todo/legacy-wire-dto-slice.md` |
| Session slice | Role as `IsHost`/`IsGuest` (the log line's role name preserved), `SessionActive`, `LocalSteamId`, `HostSteamId`, `HandshakenPeerIds`, `SessionEnded` | `SessionService` explicit `IKernelSessionFacts`; the kernel families below |
| Transport slice | `Send` / `TrySend` / `SendToAll` over `ProtocolFrame`, mapped to the one envelope message on the Runtime side | `PacketSender` explicit `IKernelFrameSender`; `KernelProtocolServiceTests` (host/guest frames) |
| Kernel authority | `IKernelCommandExecution` (`TryExecuteCommand`), `IKernelCheckpointSource` (`CreateCheckpoint`, `CurrentGlobalRevision`, `CurrentRunEpoch`), `IKernelBatchApplication` (`Apply`, `Restore`, `ResetForSession`, `BatchCommitted`) | `ItemKernelAuthority` explicit implementations; `KernelProtocolServiceTests`, `StateStreamTests` |
| Pending commands | `Track` / `ClearCommitted` / `ClearRejected` | `GuestCommandReconciliation` explicit `IKernelPendingCommands` |
| Wire codec | The mapper surface the replication path uses (batch, item, identity, data, random stream, command) plus the legacy-bridged `ToKernelItemData` | `KernelWireCodec` adapter; `KernelWireMapperTests` checkpoint round-trips |
| Checkpoint helpers | `Split` / `Assemble` take the codec | `NetworkTrafficBaselineTests`, the domain kernel tests |
| Composition | Kernel-replication block extracted unchanged | `KernelReplicationComposition`; the integration families construct the production root |

### Verification (stage 3)

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- Normative gates — 93/93 with the cycle's delivery checklist filled (while the checklist is reset
  mid-cycle the only failure is its own gate: 92/93), the direction gate included; the sync-coverage evidence matrix quotes were
  re-pointed at the moved files and their new lines (the gate fails on a quote that no longer exists,
  which is how the stale ones were found).
- Focused — the kernel families (`KernelProtocolServiceTests`, `StateStreamTests`,
  `RefusedItemCreationsTests`, `KernelWireMapperTests`, `NetworkTrafficBaselineTests`,
  `KernelSpawnPresentationProjectionTests`, `KernelCommandGatewayTests`,
  `CommandAdmissionIntegrationTests`) 71/71, plus the new layer-boundary class 11/11.
- `dotnet test CasualtiesUnknownOnline.slnx` (build included) — gates 93/93 and tests
  3 773/3 773, decomposing as 1 694 `Category=Integration` cases plus 2 079 untagged cases (the two
  filtered `--no-build` runs above). `dotnet format CasualtiesUnknownOnline.slnx` exits 0; the
  read-only form (`--verify-no-changes --include` over the changed files) also exits 0, while the
  whole-solution `--verify-no-changes` form reports only generated `obj/...​MyPluginInfo.cs` files
  (the repo's known never-clean state), never a changed file.
- **No behaviour change is claimed for the move**: the port methods delegate to the same Runtime
  members the call sites used before, the one rewritten conversion
  (`KernelProtocolCommandHandler`'s legacy item bridge) calls the same two static helpers in the same
  order, and the checkpoint helpers take the same production conversions as a parameter.

## What this does NOT prove

- **The authority half is a no-op today.** `KernelWireMapper.FromWireCommand` stamps every wire
  command kind `OwnerPredictedHostValidated`, so no production submission is refused by the authority
  rule; the mapping gap (a member's host-only wire kind is not distinguished) is inventory in
  `docs/backlog/future/strict-validation-anti-cheat.md`, not a fix in this cycle.
- **No behaviour change is claimed, and none is expected.** The moved destroy rule behaves exactly as
  before (same silence, same audit content plus the item id); the new refusals are unreachable while
  the authority is pinned, and the actor binding is already enforced by the frame validator. The
  strongest statement this cycle supports is "the seam is in place and the touched paths are green",
  not "the host now refuses new things".
- **Not every wire path passes the seam**, by design: the two protocol heals and the guest range
  request stay outside it (named above). A member's container-sync report can still make the HOST
  author a `HostOnly` destroy of a stale contained child on that member's behalf
  (`ItemContainerSyncWriter`), which is the host's own write and never reaches the seam.
- **Three of the nine named types did not move**, each with its blocker recorded and the remaining
  move filed as its own ticket (`todo/legacy-wire-dto-slice.md`): `KernelWireMapper` (its enemy-combat
  branches map the legacy protobuf messages the Game Adapter also references),
  `KernelBatchItemProjection` (its contract and its own code carry the legacy item DTOs) and
  `KernelEnvelopeHandler` (the transport side of the seam). The Application side reaches the mapper
  through `IKernelWireCodec`, so the seam is real, but the mapper itself is still Runtime code.
- **No real-client evidence.** Nothing here runs the game: the command path is verified by in-process
  simulation through the production composition root plus static reading, and the deployment identity
  check (below) proves which DLLs are installed, not how the game behaves.

## Deployment identity

Deployment is a post-commit step by design (the plugin's `ProductVersion` embeds the commit sha), so
this page records the mechanism and the handoff records the run: `tools/deploy.ps1` copies every
`*.dll` from the plugin output directory and `tools/verify-deploy.ps1` compares the deployed set with
this tree's build output and prints `ProductVersion`'s `+<sha>`. The new
`CasualtiesUnknownOnline.Application.dll` is part of that set — it reaches the plugin output through
the Runtime's transitive reference, which the review confirmed (`ls src/CasualtiesUnknownOnline.Plugin/bin/Debug/net48/`
includes it), so no deployment manifest change was needed.
