# Merge the remote-inventory design record into the design corpus

- Status: Todo
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

## Acceptance rows

| Row | The check |
|---|---|
| 1 | Every rule the page states appears on a corpus page with its decision number and a quote, and no rule is duplicated on both sides |
| 2 | No tracked document cites `docs/architecture/remote-inventory-native-parity.md` after the move |
| 3 | The corpus pages' `Gaps` carry the limits the page recorded, or say why a limit no longer applies |
