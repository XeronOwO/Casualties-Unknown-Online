# Application layer: first slice (kernel command gateway and the kernel replication move)

- Status: Todo
- Priority: Medium
- Category: Architecture / layering
- Source: Loomi architecture review (2026-09-20), item 5; absorbs the former future tickets `command-authorization-gateway.md` and `kernel-replication-namespace-relocation.md`
- Gate: starts after the unified acceptance pass (user decision 2026-09-20: foundations first, then the acceptance pass)

## Problem (evidence)

- `src/CasualtiesUnknownOnline.Runtime/CasualtiesUnknownOnline.Runtime.csproj` states the promise
  and the missing piece in one comment: the direct `GameState` reference "is an interim seam until
  the Application layer exists; the final dependency direction is Runtime -> Application ->
  GameState". Runtime today holds DI, logging, Steam, transport, persistence, mods, session
  orchestration, the adapter ports and the kernel wiring.
- **No place owns command eligibility.** `AuthorityKind` is carried by every command (`GameCommand`
  and its per-domain subclasses, 86 references across the tree), and `RejectionReason.NotAuthorized`
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

Not in this ticket: migrating the 860 Runtime files into the new layer. Only the first vertical slice
moves, and later slices migrate when they are touched anyway.
