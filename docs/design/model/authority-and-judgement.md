# Authority and judgement: who decides, who applies, on whose timeline

Every mechanic in this corpus answers the same four questions. They are stated once, here, and a mechanic page cites
this page instead of restating it.

## The actors

| Actor | What it is | What it holds |
|---|---|---|
| **Host** | the player who opened the session and runs the authoritative simulation | the kernel: every persistent gameplay fact, and the arbitration of conflicting claims |
| **Member** | every other player in the session | its own body, its own screen and input, and the projections the host sends it |
| **Kernel** | the typed deterministic state machine inside the host | one authoritative write path per fact, transactions, revisions, checkpoints |
| **World** | the simulated environment around the players | world state the host owns; projections elsewhere |

There is no dedicated server and no host migration: the host's own client is the authority while it plays.

## The two judgement sides

Two different questions are answered by two different clients, and confusing them is the defect this page exists to
prevent:

1. **Who judges whether the action happens?** The side that OWNS the thing being affected decides the outcome, using
   its own screen and its own timeline. A wound is judged by the client whose body it is; a use applied to a body is
   executed by that body's own client; an item's own state is written by the client that holds it.
2. **Who arbitrates a conflict?** The host. Two players acting on the same fact are ordered by the host's own
   arrival, never by a client's clock.

**The host computes the continuous state; the affected client judges the events.** The user's 2026-10-18 ruling for the health family states the whole shape: "the HOST computes the continuous player state by reusing the game's own methods; the affected client judges every event and applies the host's rows to its own body." A continuous quantity is the host's because it is a simulation over time; an event is the affected side's because it happened on that side's screen.`r`n`r`n**Latency is never a judgement parameter.** A rule that reads a round-trip time, or that makes a decision depend on how
fresh a peer's report is, is out of design: the affected side decides from what it can see, and the host arbitrates
from what it received.

## Authority kinds

Every operation declares one of these, and the declaration is part of its design:

| Kind | Meaning | Examples |
|---|---|---|
| **Host-validated, not predicted** | the host decides and the acting client does not predict the result | cross-player take, heal, use and carry; a member's request travels to the host and its answer travels back |
| **Presentation only** | the host applies no rule; the client shows what it was told | pushing, cosmetic overlays |
| **Client-judged, host-arbitrated** | the affected client decides, the host orders conflicting claims | the outcome reports a client sends for its own body |

A client never predicts a cross-player operation's result. Where a client acts at all, it acts on a fact it owns.

## Member reports are checked against data, never against behaviour

A fact a member REPORTS (rather than requests) is a claim. The host checks it against a fact it already holds — an
identity, a value it computed, a table it owns — refuses what contradicts that claim by name, and answers the refusal so
both sides agree. It never re-derives what it cannot reproduce (an unseeded roll, another client's frame), and it never
models what a player is able to do: reach, aim, timing and input rate are anti-cheat, which this project excludes, so a
report without a data anchor stays unbounded and is recorded as such.

## Host rules

A host rule is a setting the host alone decides. It reaches every member that reads it, and the local check it enables
is an INTERCEPTION rather than a second verdict: the member refuses the gesture it would otherwise send, and the host
still refuses the same thing if it arrives. A rule is never enforced by a client's own opinion.

## Custody invariants

- A carry relation requires a live carrier. A carried item is inside exactly one body or one container at a time.
- An item's ownership and a player's ownership stay consistent across death: dying does not duplicate or destroy an
  item's identity.
- A transfer is a custody change on the host; every client materialises its own incarnation of the item, and an
  incarnation is never the authority for the item's state.

## Records

Decision 154 (authority kinds), 155 (carry invariant), 156 (ownership consistency and death preservation), 157 (no
prediction), 161 (accept-first only for what the host can represent), 184 (the affected side judges), 241 (protocol
versioning), 263 (host rules), 265 (data validation). Domain detail: `../architecture/domains.md`.
