# State streams and snapshots

[Documentation](../README.md) > [Internals](README.md) > State streams and snapshots

---

**After this page** you can say which values a client may treat as state at all, why a reading is never
a verdict, and what happens when a projection fails. Read [The four envelopes](envelope-protocol.md)
first — this page is about what those envelopes carry once they arrive.

## Three kinds of value

Everything a client can read is one of three things, and mixing them up is the defect this page exists
to prevent:

| Kind | Who may change it | What it is good for |
|---|---|---|
| Kernel fact | only a committed batch | deciding anything: ownership, death, consumption, container contents |
| Convergent stream value | the next stream tick overwrites it | motion and presentation: position, aim, fluid volume |
| Projection or read model | nobody — it is derived and rebuildable | showing the state and answering questions about it |

## The stream is convergent-only

The state stream exists because a reliable batch per tick would be absurd. It carries values that
converge, and in exchange it accepts loss: a dropped frame costs freshness, not correctness, because
the next tick overwrites it. The rule that keeps that trade safe is written on the wire type itself —
`src/CasualtiesUnknownOnline.Protocol/Wire/WireStreamField.cs`:

```text
Streams are convergent-only: they may update existing continuous fields but never
create/destroy aggregates or change ownership.
```

A stream value therefore never creates an item, never removes an enemy, never moves ownership and never
advances a state machine. Those are facts, and facts travel as a committed batch — a value that can be
dropped must never be the thing that decides who owns something.

## What actually rides a stream

- The player and enemy state streams carry motion and presentation at a configurable cadence, 20 Hz by
  default. `src/CasualtiesUnknownOnline.Runtime/Configuration/StateStreamOptions.cs` normalizes the
  setting into the supported 1–60 Hz band.
- The fluid domain streams each member's viewport as an absolute region snapshot: a 10 Hz changed-box
  diff plus a 1 Hz full-viewport fallback.
- The periodic character snapshot — one per second — is a different mechanism with a different job: it
  is the fallback and replay channel that heals whatever the dedicated events did not deliver. In
  `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs` the report interval is
  declared as `CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)`.

## Dedicated events over snapshots

The design preference is a dedicated event for anything discrete, with the periodic snapshot behind it
as the safety net. A limb latch, an enemy bite, a consumption: each travels as its own message the
moment it happens, and the snapshot is what covers a client that missed it. The adapter's clone fact
table states the rule at the point where it matters — its enemy-bite handler is annotated "the dedicated
event — never the 1 Hz snapshot".

That ordering is not an optimisation. A discrete fact delivered by a slow periodic channel arrives late
by definition, and "late" turns into "the world disagreed for a second" once two machines act on it.

## A reading is not a verdict

A read model is exactly what it sounds like: state meant for reading. `RemoteVitalsService` and
`RemoteInventoryService`, the remote character presentation, the world-item table — all of them answer
"what does this client currently believe about that player", and all of them are one lost packet away
from being stale.

So no arbitration and no judgment may consume one. A one-second-old snapshot must never decide who owns
an item, whether a body can take a treatment, or whether a hit connected; the deciding side reads its
own live state, which is the rule in
[Who decides what happens to a player](judgment-ownership.md). Read models are for the interface, the
diagnostics and the rebuild path — not for a verdict.

## When a projection fails

Projections live outside the kernel, and the kernel refuses to let them influence it. The contract is
`src/CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth/IProjectionDomain.cs`: every projection is
a "rebuildable read model derived from an authoritative source", implementations "must never mutate
authority", and a failure must leave the domain rebuildable.

The coordinator enforces the rest — quoted from
`src/CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth/ProjectionHealthCoordinator.cs`:

```text
Projection code runs outside the kernel and must never be allowed to make a
committed batch look rejected. This coordinator wraps a projection apply,
records the last successfully applied revision, marks the domain dirty when
the projection throws, and pumps a per-domain rebuild from the kernel read
model on the Unity main thread.
```

Repeated failures escalate the domain to a degraded state an operator can see, and the authoritative
flow keeps running. The direction of repair is always the same: bring the projection back to the
committed state, never roll the committed state back to the projection.

## Related reading

- [The four envelopes](envelope-protocol.md) — the frames these values arrive in
- [Who decides what happens to a player](judgment-ownership.md) — why a reading may not arbitrate
- [The shape of CUO](architecture-overview.md) — the kernel these values derive from
- [Read game state](../how-to/read-game-state.md) — the mod-facing view of the same machinery
- [Glossary](../reference/glossary.md) — state stream, snapshot, read model, projection, checkpoint

---

[Documentation](../README.md) > [Internals](README.md) > State streams and snapshots
