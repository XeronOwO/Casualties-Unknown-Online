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

**The host computes the continuous state; the affected client judges the events.** The user's 2026-10-18 ruling for the health family states the whole shape: "the HOST computes the continuous player state by reusing the game's own methods; the affected client judges every event and applies the host's rows to its own body." A continuous quantity is the host's because it is a simulation over time; an event is the affected side's because it happened on that side's screen.

**Latency is never a judgement parameter.** A rule that reads a round-trip time, or that makes a decision depend on how
fresh a peer's report is, is out of design: the affected side decides from what it can see, and the host arbitrates
from what it received.

## Authority kinds

The kernel declares FOUR kinds, and the code is the authority for that vocabulary: `src/CasualtiesUnknownOnline.GameState/AuthorityKind.cs` lists `HostOnly`, `OwnerPredictedHostValidated`, `TriggerObservedHostCommitted` and `PresentationOnly`. Decision 157 is what leaves the two predicted kinds declared but unused: "no current cross-player operation is client-predicted". The table reads those four as three design behaviours plus one reserved pair.

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

**The admission seam judges ELIGIBILITY only, and the code states its own scope.**
`src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` is "the admission seam for a command a MEMBER
submitted over the wire: one owner for 'who may submit this'", and its scope note names exactly what it does not do: "the
gateway decides ELIGIBILITY only (who may submit), never what happens to another player's body, reach or timing. …
nothing about distance, state or sequence is judged here, and the kernel still owns every domain verdict". Ownership at the
seam means the item the member reports on is one it "may report on at all — a world item (it saw the item in the world, or
used it in place) or the member's own carried, installed or consumed item".

**A write is reported only for the reporter's OWN action, and a clone never reports.** Every capture scope in the adapter
shares one eligibility rule, `GameAdapter/Patches/CaptureScopeGuard.cs`: "a sound is reported only for THIS client's own body
action, in a plain local call. A render clone … and any remote application both stay silent, so a remote-driven mutation can
never be reported as the local player's action — the rule every write-report patch follows". A scope is also never opened
inside a remote application, because "a capture there would be meaningless (the facts travel with the intent being applied)".

**An admission is the grant, and the outcome spends it.** `Runtime/Session/PlayerInteraction/ItemActionGrants.cs` keeps "one
entry per (actor, item) the host admitted a cross-player use for on the family whose effect runs on the affected side's own
client, naming the item's owner and — for the family that is about a limb — the limb the host authorized". It exists because
"the outcome arrives from the AFFECTED side while the item belongs to somebody else, and the item domain's own rule — a member
may not report a change to another member's carried item — must keep holding", so the admission itself is the permission: "an
entry is consumed by the report it authorizes, so a repeat or a late report never lands on an item whose use is already
settled".

**"Who is mutating" is a declared scope, never an inference from the call's arguments.** `GameAdapter/CallContext.cs`: "every
game-object mutation CUO syncs runs inside a scope that declares WHO is mutating — a local player action, a remote message being
applied, a peer's intent this client executes on its own objects, or an inventory-internal reorder. Guards read `Current`
instead of inferring identity from parameter values — 'you cannot tell who you are from the parameters' was the root of the
quake-break subset bug (the numbering gate swallowed remote breaks) and the slot-drag pickup bug". The scope is process context
rather than state any domain owns, which is why it is one static owner and not a field on a service.

## The census of unbounded reports

Every report is either anchored or named unbounded, and the census is the list:
`docs/architecture/member-report-validation.md` carries one row per wire member, and a row marked `unbounded:` is a report
this project accepts without an anchor. The accepted record's reading is that the family closed what it could — "rows 1, 2
and 4 PASS; row 3 is WITHDRAWN by the ruling below" — and that the census, not a reader's memory, is where an unbounded
surface is found.

## Interaction gates are the host's

The gates that decide whether a remote interaction may happen — who may act on whom, and whether an operation is already
running — are judged by the host rather than by the asking client: the accepted record's evidence is
`InteractionGateAuthorityTests` (18 cases) plus `MedicalOperationConcurrencyTests` for the exclusivity refusal, with the
third-party chain unchanged. A client's own check stays an interception (see host rules); the decision is the host's.

**A post-interaction body snapshot is the host's, and the affected client applies it as-is.**
`src/CasualtiesUnknownOnline.GameState/Domains/Players/PlayerInteractionHealth.cs` is "a kernel-shaped post-interaction body
health snapshot … not a terminal kernel fact", and it rides the interaction result event "so the receiving Game Adapter can
apply the exact host-authoritative post-effect body state immediately" — the body's own client does not recompute what a
cross-player action did to it; it applies the result.

## Host rules

A host rule is a setting the host alone decides. It reaches every member that reads it, and the local check it enables
is an INTERCEPTION rather than a second verdict: the member refuses the gesture it would otherwise send, and the host
still refuses the same thing if it arrives. A rule is never enforced by a client's own opinion.

## What travels

The facts that cross for this model: a request and its answer, a report and its refusal, a host rule and its announcement,
and the custody a transfer moves. None of them carries a verdict the receiving side is meant to trust — that is the whole
point of checking against data.

## Custody invariants

- A carry relation requires a live carrier. A carried item is inside exactly one body or one container at a time.
- An item's ownership and a player's ownership stay consistent across death: dying does not duplicate or destroy an
  item's identity.
- A transfer is a custody change on the host; every client materialises its own incarnation of the item, and an
  incarnation is never the authority for the item's state.

## Who judges

This page IS the judgement model: the deciding side is the one that owns the thing being affected, the applying side is the client that owns the body, and the timeline is the affected side's own. The host arbitrates conflicts; it never substitutes its own frame for a decision it cannot reproduce.

## Gaps

- The anti-cheat bound is excluded by design (see the census of unbounded reports); what a future bound would anchor on is not recorded here.

## Records

Decision 154 (authority kinds), 155 (carry invariant), 156 (ownership consistency and death preservation), 157 (no
prediction), 161 (accept-first only for what the host can represent), 184 (the affected side judges), 241 (protocol
versioning), 263 (host rules), 265 (data validation). Domain detail: `../architecture/domains.md`.
