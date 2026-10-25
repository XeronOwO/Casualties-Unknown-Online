# The context menu does not block the pointer: a click on its blank area reaches the world

- Status: Todo — opened 2026-10-25 from the user's report (translated): "the right-click panel does not stop the pointer — clicking its blank
  area goes through into the world and makes the character mine".
- Priority: High
- Category: Online UI / pointer authority
- Source: user report (2026-10-25), seen on the host right after the give batch, with the guest's menu open.

## Goal

While the in-world right-click context menu is open, EVERY click inside its rectangle belongs to the menu: a click on a row runs that
row, a click on the panel's own blank area does nothing, and no click reaches the world behind it (no mining, no item use, no drag
start). The menu is a modal surface over the world for as long as it is open.

## Design

The surface is the in-world player context menu of `docs/design/surfaces/member-menu.md`, whose pointer rule is that the menu owns every
click inside its rectangle and that nothing behind it may act; the docked panel it shares its view with is
`docs/design/surfaces/quick-panel.md`, and both are one view
(`src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/OnlineUiPanelView.cs`).

## What the code says today

The panel is built with a full-rect background whose `raycastTarget` is set on purpose —
`src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/OnlineUiPanelView.cs`: "The panel is a raycast target on purpose: a click inside it
belongs to CUO's surface and must not fall through to the game's menu or the world behind it", `panel.raycastTarget = true;` — and a pin
asserts it (`OnlineUiPanelSurfacePinTests`, "a panel that lets clicks fall through to the world"). So the intent is recorded and the
graphic claims the raycast; what is NOT established is whether the game's world input asks the same question the EventSystem answers.

The world's own gate is `UIUtil.IsPointerOverUIElement(uiCasts)` over the game's `uiCasts` list (`PlayerCamera.cs:1461`,
`HandleReleaseDragging`; the mining path reads the same list before it acts). Two candidates, and the first batch decides which:

1. **The panel is not in the raycast list the game builds** — the game's list comes from its own canvas/EventSystem, and CUO's canvas is
   reached for its BUTTONS (they work) but the panel's blank area is above/below the game's own UI plane, so the raycast stops at the
   world collider behind it.
2. **The panel IS hit, but the mining path does not consult `uiCasts`** — a different entrance (a hold-to-mine timer, a direct
   `Input.GetMouseButton` read) that the pointer-over-UI check never gates.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the menu open, a click on its blank area produces no world action: a frame before and after, plus the log lines that would show mining/item use, all absent |
| 2 | A click on a row still runs that row (the menu is not made dead by the fix) |
| 3 | The same click outside the menu's rectangle still reaches the world (the block is the rectangle, not a global modal) |
| 4 | The docked quick panel behaves the same way (the two panels share one view) |

## Limits

- The first batch must capture frames: the defect is "the pointer went through", which no probe reading reports.
- A click there is an OS-level pointer action, so the batch needs the user's per-batch authorization for `send-input.ps1`; the rows
  cannot be read from the in-process driver alone.
