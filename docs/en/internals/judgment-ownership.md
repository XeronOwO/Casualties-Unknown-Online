# Who decides what happens to a player

[Documentation](../README.md) > [Internals](README.md) > Who decides what happens to a player

---

**After this page** you can explain why a verdict lives on one machine instead of on the host, and
where each half of that rule sits in the code. [Decide which side judges an
action](../how-to/decide-which-side-judges-an-action.md) is the checklist version for writing a new
feature; this page is the reasoning behind it.

## The rule, and what it costs

A judgment about what happens to a player belongs to **that player's own client**, on its own view and
its own timeline: the victim decides it was hit, the two players in an interaction judge their own line
of sight, and a client that finds its own body unable to take a treatment says so. The host keeps the
world it simulates and the [arbitration](../reference/glossary.md) of conflicting claims.

The reason is the cost of being wrong. A host verdict about somebody else's screen is paid on every
single hit: the input has to travel there and back before anything may happen, so the game feels like
the network even when nothing is contested. A rollback after a refused claim is paid only when two
players actually race, which is rare. The design therefore spends its latency budget on the rare case
instead of on every action.

## Where the gate lives

The rule is implemented as a seam rule: **the gate is evaluated by the client that forms the
request.** Every cross-player request runs the reach gate on the sending side, and no host handler
re-judges a remote actor's reach or distance. `IPlayerInteractionVisibility` is the
only gate the Runtime calls — quoted from
`src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/IPlayerInteractionVisibility.cs`:

```csharp
bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId);
```

The same interface states the direction of the decision in its type comment: the Runtime "owns the
interaction policy" and calls this narrow gateway, while the Game Adapter "owns the actual world query
(`Physics2D`/ground linecast between the two players)". A host's own action goes through the same
method, so the host is judged by its own client like everyone else.

## When the fact is on the other player's body

Some preconditions describe the target's body rather than the actor's reach — is the limb dismembered,
is there a tourniquet, how many pieces of shrapnel are live. Those cannot be answered from the host's
copy of a one-second-old report, so the host asks the target's own client. It parks the request under a
ticket and continues on the answer. Quoted from
`src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalTargetBodyGate.cs`:

```text
The target-body verdict seam of the medical operation family. The host
validates what it owns (participants, the operator's item facts, the claims),
PARKS the start request here and asks the TARGET's client; the target runs the
target half of the preconditions (<see cref="MedicalTargetBodyValidator"/>)
against its OWN live body and answers, and the parked request continues on that
answer — commit on an accept, the target's own reason on a reject.
```

The host still re-checks its own facts when the answer arrives, because the answer is one round trip
old; what it must not do is answer for the target.

## What the host keeps

- **The world it simulates.** The run seed, world generation, shared entity creation and saves are
  host facts.
- **Arbitration between claims.** Two players claiming one item is settled first writer wins, and the
  loser rolls back. The kernel has a typed vocabulary for exactly this — `RejectionReason.Conflict`
  in `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs` — so a refusal is a reason a client
  can act on, not a silent drop.
- **Eligibility, not outcome.** A command submitted over the wire has its actor bound to the transport
  sender, and the admission seam decides whether that member was allowed to submit it at all. That is
  a session-level decision, and it stops at eligibility: every domain verdict stays with the kernel.
- **Co-op has to stay possible.** An exclusive one-operator lock is the exception, not the rule; two
  operators working on one victim is normal, and their claims are arbitrated per unit rather than
  locked.

## Reports are adopted first

The host adopts and relays what a guest reports, and corrects only on an obvious conflict — a
correction never blocks the player. That default has one precondition, and it is the half people
forget: **the host must be able to represent the reported state.** A report whose content the host
cannot own (its own content set lacks the definition, the id cannot be mapped, the domain object
cannot be owned) is rejected, never accepted-but-unowned. An accepted record with no owner can never
be retracted by that owner's death, so it leaks into later snapshots and resurrects state a peer has
already destroyed. A rejection has to be visible: answer the reporter, so its re-report fallback stops,
and log the concrete mismatch.

## Latency is never an input

No judgment, arbitration or tolerance may rest on a fixed window that ignores the peer's measured
round trip. Either the deciding fact is known — a creation judged before anything acts on it, a
tombstone for a refused creation — or the judgment belongs to the client that can see it.

The distinction is between a bound that decides and a bound that gives up. Asking the target's client
needs an answer, and a silent peer must not leave the operator waiting forever, so the gate has a
liveness bound. Its own comment fixes what that bound may do: "The liveness bound is not a judgment
parameter: it only abandons a request the target never answered, so a peer that is silent leaves the
operator with an explicit refusal instead of a start that waits forever." The comment's closing
sentence repeats the rule to take away: "No measured latency, window guess or tolerance enters any
verdict." An abandoned request becomes an explicit refusal the operator can retry, not a verdict about
the target.

## What this deliberately does not do

- **It is not anti-cheat.** A client that lies about its own reach or its own body is out of scope
  until the feature set is stable; the seam can be hardened later without moving a verdict back to the
  host.
- **Local does not mean private.** A locally judged action still reports its terminal fact — death,
  unconsciousness, ownership, consumption — because those are kernel facts every machine has to
  converge on.
- **It does not turn the host into a router only.** The host still owns its world and still arbitrates;
  what it does not own is the verdict on somebody else's body, reach or timing.

## Related reading

- [The shape of CUO](architecture-overview.md) — the kernel and the layers this rule sits in
- [State streams and snapshots](state-and-snapshots.md) — why a reading is not a verdict
- [Decide which side judges an action](../how-to/decide-which-side-judges-an-action.md) — the checklist for a new feature
- [Send a message to the other players](../how-to/send-a-network-message.md) — how a report reaches the host
- [Glossary](../reference/glossary.md) — judgment ownership, arbitration, rollback, read model

---

[Documentation](../README.md) > [Internals](README.md) > Who decides what happens to a player
