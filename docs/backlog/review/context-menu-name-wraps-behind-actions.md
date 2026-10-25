# The player context menu's name is not drawn (and its empty title bar is a grey strip)

- Status: Review — code landed 2026-10-25 (`9300ee5f`, `e2910f78`, `4988c83a`, `a2091d33`); the user confirmed the result on the running build
  (translated): "there is text, and there is no grey box".
- Priority: Medium
- Category: Online UI / panel layout
- Source: user reports (2026-10-25, translated): "there is no hard cut, it WRAPS — behind the medical button", "you did not adapt to player
  name length", then "the first line of the right-click panel is a grey rectangle, I do not think it needs to stay" and "no name, the grey box
  is still there, so it is not fixed at all".

## Design

`docs/design/surfaces/member-menu.md` states the menu's parts: a title that names the player the menu is about, then the actions. The title
may be drawn by whatever machinery the surface's own controls use — the design fixes the fact (the player's name is shown with the menu), not
the widget that draws it.

## What was actually wrong (readings, not arguments)

The title bar's label carried the player's name and drew nothing. `tools/acceptance/recipes/panel-title.cs` narrowed it to TMP itself, with a
control in the same panel:

| Read (menu open, deployed `4988c83`) | Value |
|---|---|
| model title / label text | the player's name (player name and small-account suffix) in both fields |
| label rect / colour / active | 269×24, `1,1,1`, active and enabled |
| parent | `Title Bar`, under the CUO canvas (one menu object in the scene) |
| font / material | the game's own (`Retro GamingPix`) |
| `textInfo.characterCount` | **0**, and still 0 for `Test123`, `Kaguya`, `small`, `x` under `ForceMeshUpdate(true, true)` |
| **control**: a ROW label in the same panel | one CJK glyph of the game's own button text → `characterCount` **1** |

So a label built for the title never parses its string, while a row label parses normally — the component, not the text, is what fails. Two
attempts were retired by readings rather than kept on hope: the 0×TitleHeight measurement (fixed, `e2910f78`) and the font material
(`4988c83a`) were both real defects of their own but neither made the title draw.

## What landed

- The name is drawn by a **ROW** again: `OnlineUiPlayerContextMenu.Build` puts it in a label row before the actions, through the machinery
  every other line on this surface uses and that the control above proves renders (`a2091d33`).
- A panel with **no title text and no close control hides its title bar**, so the empty grey strip is gone (`a2091d33`).
- Earlier in the same ticket: the measurement is unconstrained instead of the 0×TitleHeight overload, the mesh is rebuilt once per title
  change (`e2910f78`), and programmatic labels carry the game's own font material (`4988c83a`) — and, since `a2091d33`, they are also
  cloned from the game's own label object (`e9e40636`) so the surface's other text does not depend on the failing construction path.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | The player's name is drawn whole with the menu, and nothing of it sits under the first action | **passes** — the user read the running build ("there is text, and there is no grey box", translated); a row is laid out in the panel's column, so it cannot be covered by an action |
| 2 | A name longer than the panel wraps inside the row instead of being cut, and the action buttons are not moved or covered | **passes by construction** — the row uses the same wrapping rule as every other line (the long-name case is the row layout's own, not a title-bar special case) |
| 3 | The quick panel keeps its fixed width because its title is a key | **unchanged** — that panel still passes a title; its bar keeps drawing, and its width still comes from the model |

## Limits

- The title BAR still cannot draw its own text on this build; a panel that needs a real title bar (the window's page titles are rows, the quick
  panel's is a key) works because nothing depends on that label today. If one ever does, the failing construction path is documented here and
  in `OnlineUiControlTypography`.
