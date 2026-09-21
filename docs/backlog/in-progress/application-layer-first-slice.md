# Application layer: first slice (kernel command gateway and the kernel replication move)

- Status: In progress
- Priority: Medium
- Category: Architecture / layering
- Source: Loomi architecture review (2026-09-20), item 5; absorbs the former future tickets `command-authorization-gateway.md` and `kernel-replication-namespace-relocation.md`
- Gate: **superseded 2026-09-21** — the owner's "finish every remaining todo, then come back" instruction
  replaced the earlier "starts after the unified acceptance pass" ordering (user decision 2026-09-20), so
  this ticket is worked in backlog order before the single acceptance pass.

## Problem (evidence)

- `src/CasualtiesUnknownOnline.Runtime/CasualtiesUnknownOnline.Runtime.csproj` states the promise
  and the missing piece in one comment: the direct `GameState` reference "is an interim seam until
  the Application layer exists; the final dependency direction is Runtime -> Application ->
  GameState". Runtime today holds DI, logging, Steam, transport, persistence, mods, session
  orchestration, the adapter ports and the kernel wiring.
- **No place owns command eligibility.** `AuthorityKind` is carried by every command (`GameCommand`
  and its per-domain subclasses, 86 references at HEAD `059b4eeb` and 91 in this cycle's tree —
  `grep -ro AuthorityKind --include=*.cs src | wc -l`), and `RejectionReason.NotAuthorized`
  exists in the kernel's vocabulary, but it is produced at exactly one site —
  `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemDomainModule.cs`, "item ... is not owned
  by the dropping actor". Nowhere binds an actor to the transport sender or applies a policy
  uniformly; each Runtime entry point decides for itself, and `KernelProtocolCommandHandler` is the
  only place that sees the sender and the command together.
- **Kernel replication types still sit under the item namespace** although they stopped being
  item-specific when the full-domain migration finished: `KernelProtocolService`,
  `KernelProtocolCommandHandler`, `KernelWireMapper`, `KernelDomainWireMapper`,
  `KernelStateStreamService` and `KernelBatchItemProjection` are under `Runtime/Session/Items/`,
  `KernelEnvelopeHandler` under `Session/Handlers/`, and the checkpoint surface is
  `WireCheckpointAssembler` / `GuestCheckpointReceiver`. (The relocated ticket's third example,
  `KernelSaveFileStore`, no longer exists in the tree.)

## Stages

1. **Create the layer and gate the direction.** A `CasualtiesUnknownOnline.Application` project in
   the solution, referenced by Runtime and referencing GameState and Protocol; a gate that fails if
   GameState ever references upward or if the Runtime/GameAdapter projects skip the layer.
2. **Kernel command gateway (behaviour-preserving).** Move eligibility into one place: bind the
   actor to the transport sender, apply `HostOnly` / `Owner` / `Observed` and the related policies
   centrally, and return the existing `RejectionReason` values with one uniform log/audit line that
   names the command, the actor and the reason. No new policy in this stage — only the checks that
   today live at individual entry points move behind one seam. The 2026-09-18 ruling binds the
   design: the gateway decides ELIGIBILITY (who may submit), never what happens to another player's
   body, reach or timing.
3. **Move the kernel replication surface.** Relocate the protocol/replication/checkpoint types into
   the Application layer under a neutral namespace, update references and tests, behaviour
   unchanged. If the move exposes a real responsibility split, take it; do not use the move to
   escape a file-size gate.

## Acceptance

- The direction gate fails on a synthetic GameState → Application reference and is green on the tree.
- A command that is refused today at an entry point is refused by the gateway with the same
  `RejectionReason`, pinned by a test that names the scenario; the ORDER of refusals does not change
  (an eligibility refusal must not start masking a domain refusal).
- Stage 3 is a pure move: the full suite is green and no behaviour diff is claimed.

## Notes

Not in this ticket: migrating the Runtime's 859 source files into the new layer (measured 2026-09-21:
`find src/CasualtiesUnknownOnline.Runtime -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | wc -l`;
the earlier "860" was never reproducible from the tree). Only the first vertical slice
moves, and later slices migrate when they are touched anyway.

## Progress (stages 1 and 2 landed; stage 3 open)

- **Stage 1 done.** `src/CasualtiesUnknownOnline.Application/` exists in the solution, references
  `GameState` + `Protocol` + the logging abstraction and nothing else, and the Runtime reaches the
  kernel through it: its direct `GameState` reference is gone (transitive reference through the
  layer). `ProjectDirectionGateTests` + `ProjectDirectionPolicy` declare the table (`Abstractions`,
  `GameState` and `Protocol` reference nothing; `Application` only `GameState` + `Protocol`;
  `Runtime` never `GameState` directly), read the graph from `CasualtiesUnknownOnline.slnx` (project
  references AND raw CUO assembly references), refuse an upward reference, a project above the layer
  that references GameState, a Runtime that stops referencing the layer, and a project that is
  neither declared nor listed as a consumer; the tree half is green and the control run (a direct
  `GameState` reference injected into `Runtime.csproj`) reports
  `Runtime references CasualtiesUnknownOnline.GameState, which its declared layer does not allow`,
  while the eight synthetic cases stay green.
- **Stage 2 done.** `KernelCommandGateway` (`Application/Kernel/`) is the admission seam for a
  member's MAPPED wire submission: actor-to-sender binding, the declared authority policy a member
  may not author (`HostOnly`, `PresentationOnly`), and the item-destroy eligibility rule moved
  verbatim out of `KernelProtocolCommandHandler` (its former `CanDestroy`, deleted with this change).
  Deliberately outside it, and named in the class doc: the handler's two protocol heals (a
  container-sync report and an update for an unknown carried id — they materialize the reporter's own
  carried parent, i.e. the host's own write, and the second one stamps its own authority) and a
  guest's range request, which never becomes a command. The verdict is three-valued (`Admitted` /
  `Refused` answered / `Ignored` without an answer) because the moved rule was silent, every refusal
  is audited with one uniform line naming the command, the item it names, the actor, the sender and
  the reason, and the seam sits at the POSITION the old check held so the heals and the
  creation-before-operation invariant still refuse first. An id this host never judged is therefore
  NOT reached here for a destroy (that invariant refuses it above the seam, pinned by
  `CommandAdmissionIntegrationTests`); the seam's null branch is a defensive guard so it can never
  invent a verdict for state it does not hold. The one behaviour-preserving subtlety: the wire
  command is now mapped before the destroy check runs, which is equivalent because the mapper cannot
  throw for `ItemDestroy` (`WireCommand.Identity` is non-nullable with a `new()` default) and the
  older position only mattered for that kind.
- **What stage 2 does NOT change.** Every wire kind is still mapped with
  `AuthorityKind.OwnerPredictedHostValidated`, so the authority half of the policy is structurally
  satisfied today and refuses nothing new; the gap it leaves (a member's host-only wire kind is not
  distinguished) is recorded in `docs/backlog/future/strict-validation-anti-cheat.md`, not fixed
  here, because declaring the real authority per kind is a behaviour change and this stage moves
  existing checks only.
- **Stage 3 is a design step, not a move.** `KernelEnvelopeHandler` extends the Runtime's
  `PacketHandlerBase<TPacket, TContext>` and reads `CurrentFrameLength`, and the other eight types
  take `ISessionControl`, `PacketSender`, `ItemKernelAuthority`, `RefusedItemCreations` and
  `GuestCommandReconciliation` from the Runtime; relocating them into the Application layer
  therefore needs ports for those slices first (and a decision about the packet-handler base). It
  stays for the next session, in this ticket.

## Independent adversarial review (2026-09-21, pre-commit)

FULL tier, fresh context, against the frozen working tree; report kept at
`%TEMP%\cuo-review-application-layer.md`. Result: **0 blocker / 3 major / 7 minor / 4 nit**.
Behaviour preservation and the "the new refusals are unreachable in production" pair were re-derived
independently (not taken from this cycle's tests) and survived every attack; all three majors were
defects in this cycle's SELF-DESCRIPTION, fixed in the same cycle:

- **"the single admission seam" was false** — the handler's two protocol heals (container-sync, and
  the unknown-carried update that even stamps its own authority) and a guest's range request reach the
  kernel without it. Every place that said "single" now states the MAPPED member submission and names
  the exceptions (gateway class doc, decision 210, `current.md` §7.1, the selfcheck); the
  host-authored `HostOnly` destroy a container-sync report can produce on a member's behalf is
  recorded as a limit rather than papered over.
- **the audit line lost the item id** — the moved check's only trace. The detail now carries the item
  id, pinned by `KernelCommandGatewayTests.IgnoredDestroyReport_IsAuditedWithTheItemIdItNamed`.
- **the "unjudged id is answered by the kernel" rationale was unreachable** — the
  creation-before-operation invariant refuses that input above the seam, so the seam's null branch is
  defensive. The wording is corrected and the production truth is pinned by
  `CommandAdmissionIntegrationTests.DestroyOfAnItemNobodyReported_IsRefusedByTheCreationBeforeOperationInvariant`.

Minors fixed: the dangling `CanDestroy` citation in `GuestCommandReconciliation`; four stale `todo/`
paths in `resolved/` (the reference gate exempts those records, so they had stayed green); the
direction gate's exemption-by-omission (`Abstractions` is declared, the consumers are an explicit
list, an unclassified project is refused); the unreproducible "860 Runtime files" (measured 859); the
stale `Integration` census in `test-parallelization.md` §9.1; the focused-filter decomposition in the
selfcheck. Nits: the loader now also reads raw CUO assembly references, and the Application csproj
comment says which half of its promise is gate-enforced. Not adopted: the "stray blank line after
`## Reference rules`" nit — the blank line matches that document's own style in every other section.
