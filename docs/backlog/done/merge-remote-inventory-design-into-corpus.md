# Merge the remote-inventory design record into the design corpus

- Status: Done — the rules are in the corpus and the page is retired
- Priority: Medium
- Category: Documentation / architecture–design division
- Source: the division audit in `docs/architecture/README.md` ("Misfiled pages"): `remote-inventory-native-parity.md` is a
  design record for one behaviour, not a system-shape page.

## Design

Read `docs/architecture/README.md` (the division of labour and the misfiled-pages table) and
`docs/design/AGENTS.md` (the corpus rules). The content to move belongs to `docs/design/mechanics/give-and-take.md`,
`docs/design/items/container.md` and `docs/design/model/item-custody.md`; the architecture page keeps nothing once the move
is done, because its remaining content is the behaviour's stages rather than a rule that survives a behaviour rewrite.

## Problem

The page states its own scope in its first lines: "stage 4 landed, amended 2026-10-11 — the native intent path (stage 1,
decision 218), the container family and the while-dragging body (stage 2, decision 219), the item interactions (stage 3,
decision 220) and the family audit with the acceptance preparation (stage 4, decision 221)". That is a behaviour's history
and design, and the corpus's rules say a behaviour's design lives there.

## What the move must not lose

1. The rules the four stages fix, quoted into the corpus pages (each stage's decision number stays with its rule).
2. The acceptance matrix's limits and residuals, which a reader of the give/take page needs in the page's `Gaps`.
3. The links: every document that cites the architecture page is re-pointed in the same change, and the page is retired
   rather than left as a stub — git keeps the history.

## Progress 2026-10-25 — the rules are in the corpus; the retirement is what remains

The rule extraction is done, and it went to `../design/model/item-custody.md` (a rule page, so its `Who judges` / `What travels` / `What the
player sees` / `Gaps` / `Records` sections carry them):

- **Roles and the permission** (`RemoteBackpackAccess`, decision 263) into `Who judges`, including the middle value's view-only semantics and the
  two host seams it is judged at;
- **The intent rule** into `What travels` — the wire carries the native call identity plus operands, never a CUO taxonomy; the two-sided transfer
  is the one pair with no single native call; the classification is the game's own because CUO captures the mutation call inside a one-invocation
  bracket and owns no ordering table (decision 218); the favourite mark is the local player's own and saved (decision 258);
- **The feedback split** into `What the player sees`;
- **The four limits** into `Gaps` (the two-client feel, the mutation-internal sound heard on the owner's client alone, the radial weight readout,
  the accepted no-release-animation decision 240), plus the third-peer note;
- **The decision numbers** into `Records` (155/156/161 custody, 218-221 the stages, 258 the mark, 240 the accepted limit, 263 the permission).

What is NOT yet done, and is the rest of this ticket:

1. The page's mechanism inventory (§2, the native drag pipeline with its R1-R14 / W1-W4 table and the mutation-side guards) is EXTRACTED but not
   re-homed: it is native reference material the corpus has no page for. Either `../design/mechanics/give-and-take.md` grows the release table it
   cites, or a `mechanics/` page is created for it — the decision is owed before the page is deleted, because deleting first would lose the
   `reversing/` anchors that the intent names are keyed to.
2. The page's §3.6 deletions, §4 stage table and §5 adjudicated tickets are history: they belong to the decision records and the done tickets,
   not to a design page, and they are dropped rather than moved (the decisions keep them).
3. Then: delete `docs/architecture/remote-inventory-native-parity.md`, re-point the four tracked documents that cite it as CURRENT
   (`architecture/README.md` twice, `architecture/surfaces.md`, `design/items/container.md`, `design/audit/README.md`) and the census rows; the
   ~140 citations inside `backlog/done/`, `evidence/` and `decisions/` are historical records and stay as they are — a done ticket citing the page
   it was designed against is a fact about that cycle, not a live link, and the census reports them under their own rows.

## Resolution (2026-10-25) — the page is retired

The move is complete, in two commits:

1. **The rules went into the corpus.** `../design/model/item-custody.md` gained the roles and the `RemoteBackpackAccess` permission in `Who
   judges`, the intent rule and the classification rule in `What travels`, the feedback split in `What the player sees`, the four limits in
   `Gaps`, and the decision numbers in `Records`. The native REFERENCE material — the drag pipeline's R1-R14 / W1-W4 table, the callee guards,
   the capture bracket and the intent vocabulary — became a corpus page of its own,
   [`mechanics/remote-inventory-entries.md`](../design/mechanics/remote-inventory-entries.md), because it is the table the intent names are keyed
   to and it had no other home.
2. **The architecture page is deleted**, not stubbed: `docs/architecture/remote-inventory-native-parity.md` is gone, and the two tracked
   documents that cited it as current (`architecture/README.md` twice, `architecture/surfaces.md`) now point at the corpus page. Its census row
   went with it, and the two rows that were reached THROUGH it (`architecture/projection-framework.md`, the stage-4 audit) are reached by the new
   page instead, which names both.

Rows: 1 — every rule the page states is on a corpus page with its decision number, and nothing is duplicated across the two ✔. 2 — no tracked
document cites the deleted page ✔ (the ~140 citations inside `backlog/done/`, `evidence/` and `decisions/` are historical records of the cycles
that were designed against it and stay as they are; the census reports them under their own rows). 3 — the limits are in the model page's
`Gaps`, each with its decision number where one exists ✔.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | Every rule the page states appears on a corpus page with its decision number and a quote, and no rule is duplicated on both sides |
| 2 | No tracked document cites `docs/architecture/remote-inventory-native-parity.md` after the move |
| 3 | The corpus pages' `Gaps` carry the limits the page recorded, or say why a limit no longer applies |
