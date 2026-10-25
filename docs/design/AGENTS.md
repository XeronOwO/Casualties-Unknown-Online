# docs/design — the game as a game

The **design corpus** of CUO: what the player can do, what the rules are, what the player sees, and what governs each
surface. `docs/architecture/` says how the system is built, `docs/decisions/` records the rulings, `docs/backlog/`
carries the work; this corpus says **what the product is**.

## How a page's facts are sourced

No page is written from recollection. Every claim is one of exactly three kinds, and it carries its source on the page:

- **a record**: the ticket and decision that fix the behaviour, linked by path (an accepted ticket in
  `docs/backlog/done/`, an open one in `todo/`/`review/`, or a numbered decision in `docs/decisions/`);
- **a fact of the game**: quoted from the decompiled tree by file and line;
- **an observation**: what a player or a run reads, with where it was read and when.

Where two records disagree, the page states WHICH one is current and why — a later decision supersedes an earlier one, an
accepted ticket supersedes the proposal it replaced, and a rule in the root `AGENTS.md` supersedes both. A conflict that
cannot be resolved from the records is a `GAP`, never a choice made by the writer.

## What a change reads

The mechanic page states the player's action and the rules; the item or surface page states the entry and what it must
reproduce; the model pages state which side judges and which native call is mirrored. A change reads those pages **before**
it starts and names them in its ticket (the delivery checklist's *Design comprehension* box).

Every page follows `docs/AGENTS.md`: English, one question per page, no machine paths, no secrets, and **results only** —
the design as it stands, never the history of how it was reached, never code.

## Reading order for a change

| Step | Read | Answer in the ticket |
|---|---|---|
| 1 | `model/authority-and-judgement.md` | which side judges, on whose timeline |
| 2 | `mechanics/<mechanic>.md` | the action, the rules, the refusals |
| 3 | `items/…` or `surfaces/…` | the native call the entry mirrors |
| 4 | the record links there | the ruling and the accepted ticket |
| 5 | `../architecture/surfaces.md` row | the authority, and what must NOT change |

A missing page is a `GAP`: recording the design is then the first work item, and the change waits for it.

## Layout

| Path | Holds |
|---|---|
| `model/authority-and-judgement.md` | actors, judgement sides, authority kinds, member reports, host rules, custody invariants |
| `model/item-custody.md` | item identity, custody, incarnations, what travels |
| `model/session-lifecycle.md` | lobby, run, cut, continue, rejoin |
| `mechanics/README.md` | the mechanic taxonomy |
| `mechanics/item-use.md` | applying an item to a teammate |
| `mechanics/give-and-take.md` | handing over and taking an item |
| `mechanics/console.md` | the command and chat surface |
| `mechanics/medical-view.md` | treating a teammate's body |
| `mechanics/cpr-and-revival.md` | compressing a patient, revival |
| `mechanics/sleep.md` | sleep, forced sleep, sleeping bodies |
| `mechanics/backpack-view.md` | a teammate's pack on your screen |
| `mechanics/layer-advance.md` | ending a layer, the boundary |
| `mechanics/death-and-drops.md` | dying, drops, the corpse, revival |
| `mechanics/spectator.md` | following a teammate after death |
| `items/README.md` | item families and what their own actions do |
| `items/food.md` | eating: one bite per use, what the food's own action does |
| `items/drink.md` | drinking: the container's own drink path, a non-drink refused unrun |
| `surfaces/` | one page per surface (`GAP`) |
| `cross-cutting/localization.md` | one language per reader |
| `cross-cutting/permissions-and-commands.md` | the command family and the host rules behind it |
| `cross-cutting/observability.md` | what a run leaves behind |
| `cross-cutting/saves-and-recovery.md` | what a save holds, what a restore may do |
| `cross-cutting/` (remaining) | — |

## The page template

Every page carries these sections in order; a section that does not apply says why in one line, never silently dropped.

1. **What the player does** — the input sequence, the screen, the expectation.
2. **What the game itself does** — the native call mirrored (`reversing/` file:line) and the state it depends on.
3. **Rules and states** — values, thresholds, transitions.
4. **Refusals** — each condition with the sentence the player reads, by key.
5. **Who judges** — the deciding side, the applying side, the timeline.
6. **What travels** — the facts crossing between clients.
7. **What the player sees and hears** — the visible and audible result.
8. **Records** — the decisions and accepted tickets.
9. **Gaps** — what is not yet designed or recorded.

## Related reading

- `../architecture/surfaces.md` — the surface index this corpus expands.
- `../architecture/current.md`, `../architecture/domains.md` — how the system is built.
- `../development/native-parity.md` — the standard native-mirroring behaviour is held to.
- `../decisions/index.md` — the decision register.
