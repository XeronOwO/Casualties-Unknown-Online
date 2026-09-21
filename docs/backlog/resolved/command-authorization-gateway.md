# Command authorization gateway

- Status: Resolved (absorbed 2026-09-20 into `in-progress/application-layer-first-slice.md`)
- Category: Architecture / authority / security
- Source: Loomi architecture review (2026-09-04)

## Disposition

Absorbed, not implemented standalone. The 2026-09-20 review re-checked the premise against the tree
and it still holds: `AuthorityKind` is carried by every command (`GameCommand` and its per-domain
subclasses, 86 references) and `RejectionReason.NotAuthorized` exists in the kernel's vocabulary,
but it is produced at exactly one site — `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemDomainModule.cs`,
"item ... is not owned by the dropping actor" — and nothing binds an actor to the transport sender
or applies eligibility uniformly.

What changed is ownership, not validity. The same review assigns session-level permission to the
Application layer, and that layer is itself a promised seam
(`CasualtiesUnknownOnline.Runtime.csproj`: the direct `GameState` reference "is an interim seam until
the Application layer exists"). So the gateway became stage 2 of
`in-progress/application-layer-first-slice.md`: one place that binds the actor to the transport sender,
applies `HostOnly` / `Owner` / `Observed` centrally and returns the existing `RejectionReason`
values with a uniform audit line.

## Constraint carried forward

The 2026-09-18 ruling binds the design: a judgment about what happens to a player belongs to that
player's own client, and the host owns only the world it simulates and the arbitration of
conflicting claims. The gateway therefore decides ELIGIBILITY (who may submit a command), never what
happens to another player's body, reach or timing.
