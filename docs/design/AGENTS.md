# docs/design — the game as a game

This directory is the **design corpus** of CUO: what the player can do, what the rules are, what the player sees, and
what governs each surface. It is the layer the other records do not cover — `docs/architecture/` says how the system is
built, `docs/decisions/` records the numbered rulings, `docs/backlog/` carries the work. This corpus says **what the
product is**.

## Why it exists

A change that starts from the source files at hand is a change that will drift from the design, however carefully it
compiles. The corpus is the thing a change is read against **before** it starts:

1. the mechanic page says what the player does and what the rules are;
2. the item or surface page says what the entry is and what it must reproduce;
3. the authority rows say which side judges and which native call the behaviour mirrors;
4. the record links say where the ruling came from.

Every page in this corpus follows `docs/AGENTS.md`: English, one question per page, no machine paths, no secrets, and
**results only** — a page states the design as it stands, never the history of how it was reached, and never code.

## Reading order for a change

| Step | Read | Answer in the ticket |
|---|---|---|
| 1 | `model/authority-and-judgement.md` | which side judges this, and on whose timeline |
| 2 | `mechanics/<mechanic>.md` | the player's action, the rules, the refusals |
| 3 | `items/<family>.md` or `surfaces/<surface>.md` | the native call the entry mirrors, and its input sequence |
| 4 | the record links on those pages | the ruling number and the accepted ticket the row cites |
| 5 | `../architecture/surfaces.md` row | the authority, and what the change must NOT alter |

A page that does not exist yet is a `GAP`: reading and recording the design is then the first work item, and the change
waits for it.

## Layout

| Path | Holds | Status |
|---|---|---|
| `model/authority-and-judgement.md` | who decides, who applies, on whose timeline; authority kinds; member reports; custody invariants | lands |
| `model/item-custody.md` | item identity, custody, incarnations, what travels | lands |
| `mechanics/README.md` | the mechanic taxonomy and what a mechanic page must state | seeded |
| `mechanics/item-use.md` | applying an item to a teammate: entries, families, refusals, judging side | seeded |
| `mechanics/give-and-take.md` | handing an item to a teammate and taking one from their pack | seeded |
| `mechanics/console.md` | the command and chat surface: entry keys, live candidates, answers | seeded |
| `items/README.md` | the item families and what each family's own action does, with the measured facts the corpus keeps | seeded |
| `surfaces/` | one page per player-facing surface; `../architecture/surfaces.md` is its index | GAP |
| `cross-cutting/localization.md` | one language per reader, whole sentences, keys on the wire | seeds |
| `cross-cutting/` (remaining) | observability, permissions and recovery | GAP |

## The page template

Every mechanic, item and surface page carries these sections, in this order. A section that does not apply says why in
one line; it is never dropped silently.

1. **What the player does** — the exact input sequence, what is on screen, what is expected to happen.
2. **What the game itself does** — the native call the entry mirrors, quoted from the decompiled tree by file and
   line, and the state it depends on (the hand, a body slot, a container, time).
3. **Rules and states** — the values, thresholds, states and transitions the behaviour has.
4. **Refusals** — every way the action can be refused, with the condition and the sentence the player reads (by
   localization key).
5. **Who judges** — the side that decides, the side that applies, and the timeline the decision uses.
6. **What travels** — the facts that cross between clients, in the corpus's own words (not the wire structs).
7. **What the player sees and hears** — the visible and audible result, at the moment it happens.
8. **Records** — the decisions and accepted tickets that fix this design.
9. **Gaps** — what is not yet designed or not yet recorded, named plainly.

## Related reading

- `../architecture/surfaces.md` — the surface index this corpus expands.
- `../architecture/current.md`, `../architecture/domains.md` — how the system is built.
- `../development/native-parity.md` — the standard a native-mirroring behaviour is held to.
- `../decisions/index.md` — the decision register and its traceability index.
- `../backlog/README.md` — the work items that land a page's gaps.
