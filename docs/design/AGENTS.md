# docs/design — the game as a game

The **design corpus** of CUO: what the player can do, what the rules are, what the player sees, and what governs each
surface. `docs/architecture/` says how the system is built, `docs/decisions/` records the rulings, `docs/backlog/`
carries the work; this corpus says **what the product is**.

## How this corpus divides work with `docs/architecture/`

Architecture holds the abstract model — layers, kernel, domain ownership, protocol envelopes, invariants — and a page
there stays true even if every player-facing behaviour were rewritten. This corpus holds the concrete behaviour: inputs,
rules, thresholds, refusals, the native call a gesture mirrors, who judges, what travels. A behaviour that appears on both
sides belongs here, with the architecture page keeping only the rule; a misfiled page is moved, never copied.

## How a page's facts are sourced

No page is written from recollection. Every claim is one of three kinds and carries its source on the page:

- **a record** — the ticket and decision that fix the behaviour, linked by path (`docs/backlog/done|review|todo/…`,
  `docs/decisions/…`) and **quoted**, so the grounding is visible without opening the ticket;
- **a fact of the game** — quoted from the decompiled tree by file and line;
- **an observation** — what a run read, with where and when.

Where two records disagree, the page says WHICH is current and why: a later decision supersedes an earlier one, an
accepted ticket supersedes the proposal it replaced, and the root `AGENTS.md` supersedes both. What the records cannot
settle is a `GAP`, never a choice made by the writer.

## The page template

Two kinds of page. A section that does not apply says why in one line — it is never silently dropped, and
`DesignPageSectionsGateTests` refuses a page missing one of its kind's sections.

**A behaviour page** (mechanic, item family, surface) carries nine sections:

1. **What the player does** — the input sequence, the screen, the expectation.
2. **What the game itself does** — the native call mirrored and the state it depends on. A surface CUO owns says **What it
   is** instead, because it mirrors no native call.
3. **Rules and states** — values, thresholds, transitions. A surface may say **Parts and states**.
4. **Refusals** — each condition with the sentence the player reads, by key.
5. **Who judges** — the deciding side, the applying side, the timeline.
6. **What travels** — the facts crossing between clients.
7. **What the player sees and hears** — the visible and audible result.
8. **Records** — the decisions and accepted tickets.
9. **Gaps** — what is not yet designed or recorded.

**A rule page** (the model and the cross-cutting rules) opens by saying what it governs, then carries its rules under
whatever headings those rules need, and four fixed sections: **Who judges**, **What travels**, **Records**, **Gaps**.

## What a change reads

The mechanic page states the player's action and the rules; the item or surface page states the entry and what it must
reproduce; the model pages state which side judges and which native call is mirrored; `../architecture/surfaces.md` gives
the surface's authority. A change reads those pages **before** it starts and names them in its ticket (the delivery
checklist's *Design comprehension* box). A missing page is a `GAP`: recording the design is then the first work item.

## Related reading

- `INDEX.md` — every page, one line each.
- `../architecture/surfaces.md` — the surface index this corpus expands.
- `../development/native-parity.md` — the standard native-mirroring behaviour is held to.
- `../decisions/index.md` — the decision register.
- `../evidence/delivery-checklist.md` — the boxes that enforce this corpus per cycle.
