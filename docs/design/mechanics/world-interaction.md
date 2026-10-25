# World interaction: hitting, breaking and damaging the world

What a player does to the world around them — mining a block, chipping it, setting something off — and how that reaches
the party.

## What the player does

Hits a block repeatedly. Cracks appear as its accumulated damage rises; at the threshold the block breaks, drops what it
carried, and disappears for everyone. A block that is half-damaged keeps its cracks, so a player can leave a hole started
and finish it later.

## What the game itself does

The game keeps ONE live list of blocks that carry partial damage — its own `WorldGeneration.world.blockDamages` — with a
capacity of 128 entries and an eviction policy: when it is full it evicts the OLDEST entry and keeps the newest. Eviction
is not a silent forget: the accepted record states that the evicted entry's `damage` is set to `1E+10f` and `UpdateSprite()`
runs first, and the damage component routes `damage/health >= 1f` to `Object.Destroy(spr.gameObject)` — so "the cell's
crack sprite is destroyed and its accumulated damage restarts from zero on the next hit".

The list also holds damage CUO never observed: the direct `DamageBlock(Vector2Int)` callers — "footstep crushing, the spider
burrow — are not hooked".

## Rules and states

- **One table, not two.** An earlier CUO registry kept a second list of the same cells with a different capacity and the
  opposite eviction policy, so "past 128 live damaged cells the two tables held DIFFERENT cells: the game kept the newest
  128, CUO kept the first 256 it was told about". The user's decision on it was "option 1 — align to the game's own policy …
  and do it by REMOVING the second table", chosen because "giving the registry the game's cap and the game's eviction would
  have left two copies of one set with a hand-maintained agreement between them".
- Capacity, eviction, enumeration order and damage written by the unhooked callers therefore all follow from the game's own
  table.
- A late joiner receives the host's cells IN THE GAME'S ORDER, not a dictionary's: the earlier snapshot's order "was not the
  game's order at all", and a receiver that applied rows until its own cap refused them held "a different set from the
  host's newest-128 in almost every case".
- **A trap has five phases, and the kernel records the TRANSITIONS rather than owning the mechanism.**
  `src/CasualtiesUnknownOnline.GameState/Domains/WorldEntities/TrapPhase.cs` declares `Armed = 1`, `Warning = 2`,
  `Triggered = 3`, `Cooldown = 4`, `Disabled = 5`, and its own summary says "the kernel records observed transitions; the game
  adapter remains the source of the actual native transition" — so a phase the party sees is a reported fact, never a CUO
  simulation of the trap.
- **First writer wins.** Two players breaking the same cell do not both get it: the host accepts the first report and
  refuses the second, and the acceptance reading is "two senders broke one cell in both orders and once concurrently; the
  host refused the second report".
- **A break and its drops are ONE message, so they get one verdict.** `BlockDamagedMsg` carries the drops when the damage
  broke the block, "the break and its drops are ONE message, so the host's arbitration gives them one verdict:
  first-writer-wins (the accepted report's drops register and materialize everywhere; the rejected report's drops are rolled
  back on the breaker via `ItemReject`)". A drop "the destroyed block's own table cannot produce is refused ON ITS OWN while
  the rest of the set is relayed", because "the drops are the breaker's local roll, the host cannot re-derive that roll (its
  randomness is the process-global unseeded `UnityEngine.Random`)".
- **A break clears the stale crack entry.** A direct `SetBlock(0)` path must clear the game's stale `BlockDamage` entry and
  its sprite, or the host keeps "ghost fragments" of a block the member already mined — the accepted note for that defect is
  "direct `SetBlock(0)` paths now clear the game's stale `BlockDamage` entry/sprite".
- **Partial damage is accounted PER SENDER.** Each arrival carries its sender's cumulative contribution and the host merges
  per sender, so an absolute report and a live delta cannot double-count the same hit: the landing record's words are
  "partial damage is accounted PER SENDER on the host, every arrival carries its sender's cumulative contribution".
- **Partial damage is kept absolutely and merged per cell.** The member holds each reported cell's ABSOLUTE damage, the
  fallback re-reports the outstanding set, and the host merges per cell and answers every reported cell authoritatively: "a
  zero answer clears a row this host's own cap/range rules refused", and the accepted reading is that "the swallowed
  re-report healed on the steady 60 s step, two senders summed to 2 (never the maximu…)".
- **A destruction report's drops are BOUNDED by the destroyed thing's own table, never re-rolled.**
  `src/CasualtiesUnknownOnline.Runtime/Session/World/DropBoundPolicy.cs` states why the weaker check is the correct one: the
  game rolls drops with `UnityEngine.Random` inside `WorldGeneration.DamageBlock` and `BuildingEntity.Update` — "a
  process-global, UNSEEDED stream that everything else in that process advances (sky colour, footsteps, world generation,
  earthquakes). The host cannot know where the reporter's stream stood, so which item, how many and which condition a
  legitimate roll produced cannot be re-derived. A check that re-rolled would refuse ordinary play." The bound is therefore
  "the report must lie inside the destroyed thing's own table", the rule is one-sided ("a MISSING drop is the reporter's own
  loss and is never refused, while an ADDED one is the injection this exists to stop"), and both message shapes — a block
  break's drops and the building-death drops that ride the same report — go through one rule rather than two matchers.
- **The world-entity table is four facts, not one.** `src/CasualtiesUnknownOnline.GameState/Domains/WorldEntities/WorldEntityState.cs`
  is an immutable snapshot of four lists — trap consumptions, building-entity health, opened entities and trap states — and a
  trap's one-shot consumption is the shape `TrapConsumptionFact` (position, an opaque kind, kind-specific `Extra`, and the
  trigger time), which is also the row a re-report can be matched against.
- **Placing a block is heard too.** A placed block's own sound reaches the others, not only the placer: the accepted record
  closes the case where "the host's … block place sound" was not synced to the guest.
- **The party hears the hits and the break.** A block's hit and break sounds are replayed to the others as they happen; the accepted reading judged "five hits, the break, the reverse direction, the third peer and the two-player cell" for that family.- **The fallback's first resend is a flat 60 s.** A pending report that has not been acknowledged is re-sent on a fixed cadence, and the accepted measurement is precise: "entry-phase re-report 5.04 s, the acknowledged control 57 ms with no duplicate, the steady re-report 60.06 s", with the runtime-entity and backwards-clock cases pinned by named tests.
- **A member's mutation is re-reported.** A world-block change a member made is re-reported periodically rather than kept alive by one message, so a swallowed report heals: the accepted live reading blacked out the host's inbound, the guest broke a cell, and after the blackout ended the host still read the old value at +12 s and **`0` at +67 s** — "the swallowed report healed by the fallback re-report".- The re-report ticket's own state note is the corpus's example of a superseded reference: it says "CUO's own partial-damage registry was DELETED (`done/block-damage-table-capacity-alignment.md`)" and that "references to `BlockDamageRegistry.cs` below are historical" — the later decision is the current design, and this page follows it.- A long run may lose mining progress on its oldest half-damaged cells — that is the game's own eviction, not a CUO defect,
  and the corpus records it so a reader does not mistake it for one.

## Refusals

| Condition | The player reads |
|---|---|
| a break report for a cell another player already broke | the second report is refused by the host; the cell is already gone |
| a damage report for a cell beyond the cap | the game's own eviction applies; nothing is refused to the player |

## Who judges

The host owns the world's damage and its table. A member's hit is a REPORT: the host checks it against the cell's state it
holds and accepts the first writer for a cell. A late joiner is given the host's cells rather than deriving them.

## What travels

Damage reports from the client that hit the block, the host's table rows (with the game's own order) to a joining client,
and a break's drop.

## What the player sees and hears

Cracks that deepen with damage, the block breaking with its own sound and drop, and the same holes for every player.

- **A trap's action and its drops agree on every client.** A trap that acts and a trap that drops are the same fact: the destruction's drops carry the same ids on host, guest and a third client ("the same drop ids on host, guest and the third client"), and an action/entity divergence is contained at the Runtime seam rather than left to reconcile later.
## The layer's hazards

A hazard the layer imposes is the host's, and every client shows the same one: the radiation line descends at the HOST's rate between resends rather than at each client's own ("The radiation line descends at each client's own rate between the host's resends", fixed and read on the machine: with the member unconscious and the host conscious both lines advanced together, "delta 6.142 vs 6.174 over the same window" instead of the member's own `0.3` units/s), and the line stops above the layer floor leaving a shared margin rather than pinning to the floor.

## Records

Accepted: `../../backlog/done/block-break-first-writer-wins.md` (the dual-side reading), `done/block-damage-table-capacity-alignment.md`
(the one-table decision), `done/block-break-log-names-the-wrong-shape.md` (the diagnostic line that names which shape ran).

## Gaps

- Traps, locks and mines are world interactions of their own and are not covered here yet; their rows live in the save and
  restore family of decisions.
- The exact bound on a damage report, and what a client does with a refused one, is not recorded here yet.
