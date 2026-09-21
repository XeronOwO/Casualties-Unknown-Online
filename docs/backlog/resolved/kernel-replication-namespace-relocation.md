# Kernel replication namespace relocation

- Status: Resolved (absorbed 2026-09-20 into `in-progress/application-layer-first-slice.md`)
- Category: Architecture / maintainability
- Source: Loomi architecture review (2026-09-04)

## Disposition

Absorbed, not implemented standalone. Re-checked against the tree on 2026-09-20: the premise holds —
the kernel protocol/replication types are still under `Runtime/Session/Items/` although they stopped
being item-specific when the full-domain migration finished (`KernelProtocolService`,
`KernelProtocolCommandHandler`, `KernelWireMapper`, `KernelDomainWireMapper`,
`KernelStateStreamService`, `KernelBatchItemProjection`), with `KernelEnvelopeHandler` under
`Session/Handlers/`.

One correction to the ticket as written: its third named type, `KernelSaveFileStore`, no longer
exists in the tree. The checkpoint surface is now `WireCheckpointAssembler` and
`GuestCheckpointReceiver`, also under `Session/Items/`.

It was merged rather than kept because a standalone move would be paid twice: the Application layer
takes that surface anyway (the 2026-09-20 review's item 5 gives it the "Kernel ↔ Protocol mapping
entry"), so the relocation is one mechanical step of that work — stage 3 of
`in-progress/application-layer-first-slice.md` — and not a work item with independent value.

## Constraint carried forward

The relocation is a move, not an architecture pass: update references and tests, keep behaviour
unchanged, and take a real responsibility split only if the move exposes one. The original ticket's
warning stands — do not use the move to escape a file-size gate.
