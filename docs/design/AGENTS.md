# docs/design — the game as a game

The **design corpus**: what the player can do, what the rules are, what the player sees, who governs each surface.
`docs/architecture/` says how the system is built, `docs/decisions/` records the rulings, `docs/backlog/` carries the work;
this corpus says **what the product is**. A behaviour that appears on both sides belongs here, with the architecture page keeping
only the rule; a misfiled page is moved, never copied.

## Where a page's facts come from

No page is written from recollection. Every claim is one of three kinds and carries its source on the page:

- **a record** — the ticket and decision that fix the behaviour, linked by path and **quoted**;
- **a fact of the game** — quoted from the decompiled tree by file and line;
- **an observation** — what a run read, with where and when.

Where two records disagree the page says WHICH is current and why (a later decision supersedes, an accepted ticket supersedes the
proposal it replaced, the root `AGENTS.md` supersedes both). What the records cannot settle is a `GAP`, never a choice by the writer.

## The page template

Two kinds of page; a section that does not apply says why in one line — it is never silently dropped, and
`DesignPageSectionsGateTests` refuses a page missing one of its kind's sections.

**A behaviour page** (mechanic, item family, surface) carries nine sections: 1 **What the player does** (input sequence, screen,
expectation) · 2 **What the game itself does** (the native call mirrored and the state it depends on; a surface CUO owns says **What
it is** instead) · 3 **Rules and states** (values, thresholds, transitions; a surface may say **Parts and states**) · 4 **Refusals**
(each condition with the sentence the player reads, by key) · 5 **Who judges** (deciding side, applying side, timeline) · 6 **What
travels** · 7 **What the player sees and hears** · 8 **Records** · 9 **Gaps**.

**A rule page** (the model, the cross-cutting rules) opens by saying what it governs, then its rules under whatever headings they
need, plus four fixed sections: **Who judges**, **What travels**, **Records**, **Gaps**.

## What a change reads

The mechanic page states the player's action and the rules; the item or surface page states the entry and what it must reproduce;
the model pages state which side judges and which native call is mirrored; `../architecture/surfaces.md` gives the surface's
authority. A change reads those pages **before** it starts and names them in its ticket (the delivery checklist's *Design
comprehension* box). A missing page is a `GAP`: recording the design is then the first work item.

## Related reading

`INDEX.md` — every page, one line each · `../architecture/surfaces.md` — the surface index this corpus expands ·
`../development/native-parity.md` — the standard for mirrored behaviour · `../decisions/index.md` — the register ·
`../evidence/delivery-checklist.md` — the boxes that enforce this corpus per cycle
