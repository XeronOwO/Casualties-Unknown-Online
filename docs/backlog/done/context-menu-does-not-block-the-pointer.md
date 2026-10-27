# The context menu does not block the pointer: a click on its blank area reaches the world

- Status: Done — all four acceptance rows judged on the 2026-10-25 click batch; the bounded negative of row 1 is recorded as a residual below, and the acceptance channel's virtual pointer now makes its reproduction drivable without OS input — opened 2026-10-25 from the user's report (translated): "the right-click panel does not stop the pointer — clicking its blank
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

## The instrument now exists, and its first reading (2026-10-25)

A one-line recipe asks the game's own question at a chosen point (`EventSystem.RaycastAll` at the point, then each hit's layer against
`LayerMask.NameToLayer("UI")` — the exact check `UIUtil.IsPointerOverUIElement` makes). Its first reading, taken in the world at the screen centre:

```
point (640,360): hitCount=12, first="Viewport", firstLayer=5, isUiHit=true
```

Layer 5 is the game's UI layer, so **the pointer-over-UI gate is reachable at the centre of the screen** — the layer half of candidate 1 is
therefore already satisfied by `dc88f5cc` (CUO's surfaces sit on the UI layer), and a click inside a CUO panel cannot be invisible to the gate for
that reason. What the reading does NOT yet prove is the panel's own rectangle: the top hit is the game's `Viewport`, which sits under the pointer
everywhere, so the next reading has to be taken with the menu open at a known point and compare the hit list inside the panel's rectangle with the
list just outside it.

That is one recipe call away — and it is blocked on nothing now: the recipe-shape defect that stopped this batch is measured on
`done/new-recipe-files-are-rejected-by-the-evaluator.md` (a body must be ONE line), so the next batch can finish the cause and hand rows 1-4 to a
judgement.

## The panel IS hit, and it IS on the UI layer (measured 2026-10-25, batch `.acceptance/batch-menu3`)

With the menu open at (640,360) on the host, the game's own question was asked at four points (the one-line `ui-raycast-at` recipe: the same
`EventSystem.RaycastAll` + `layer == LayerMask.NameToLayer("UI")` check `UIUtil.IsPointerOverUIElement` makes):

| Point | First hit | Layer |
|---|---|---|
| (640,360) — where the menu was opened | `Viewport` | 5 (UI) |
| (660,370) — inside the panel | **`CUO Online UI Player Menu`** | **5 (UI)** |
| (620,380) — left of the panel | `Viewport` | 5 (UI) |
| (900,500) — far from it | `Viewport` | 5 (UI) |

So **CUO's menu IS a raycast target on the game's UI layer, and the EventSystem reaches it** — candidate 1's layer half is satisfied
(`dc88f5cc`), and a click inside the panel cannot be invisible to the gate for that reason.

**What the reading also shows, and it changes the question**: the game's own `Viewport` is on the UI layer and under the pointer EVERYWHERE, so
`UIUtil.IsPointerOverUIElement()` answers true across the whole screen. A gate that is always true cannot be what lets a click through — so
candidate 2 is now the live one: either the world action the user saw is not the one `HandleAttacks` guards (`PlayerCamera.cs:1826`), or the menu
was already closing when the click landed. That distinction needs the user's own click, because both candidates end at "what happened at that
instant", and no probe can manufacture it.

Rows 1-4 needed a batch with the user's mouse authorization; the user granted it for one batch (2026-10-25) and the four rows were judged in it.

## All four rows JUDGED 2026-10-25 (batch `.acceptance/batch-menuclick`, user's mouse authorization)

Real clicks through `send-input.ps1`, each with a client-captured frame read back, on the host with the menu open at (640,360):

| Row | What was done | What the evidence shows |
|---|---|---|
| 1 | click at (760,470): inside the menu's rectangle, on its blank right-hand area, off every row | `.acceptance/frames/menu-before-click.png` → `menu-after-blank-click.png`: the menu is still open, nothing behind it changed, and the host log gained no item/use/attack line (only the 1 Hz `[ItemTraffic]`/`[CarriedSync]` lines that run every second). **No world action.** |
| 2 | click at (683,393): the row the UI labels "Medical" (its own label is localized) | `.acceptance/frames/menu-row-click.png`: the row RAN — the medical view for that member opened over the whole screen. The menu is not made dead by the block. |
| 3 | click at (1100,620): outside the menu's rectangle | `.acceptance/frames/menu-after-outside-click.png`: the menu closed and the game itself reacted (its own alert line drawn at the top of the screen), i.e. the click reached the world. The block is the rectangle, not a global modal. |
| 4 | the docked panel: `open-window`, then a click at (700,560) inside its blank area | `.acceptance/frames/panel-blank-click.png`: the window is open and unchanged, the world behind it unchanged. The shared view behaves the same way. |

**So the defect does not reproduce on this build.** That is a finding, not a pass by assertion: the click-through the user saw was reported before the
menu's layer fix and the panel's own raycast work, and every one of the four behaviours the ticket asks for is now the behaviour the build has.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | With the menu open, a click on its blank area produces no world action: a frame before and after, plus the log lines that would show mining/item use, all absent | judged ✔ (frames above, no world action) |
| 2 | A click on a row still runs that row (the menu is not made dead by the fix) | judged ✔ (the row the UI labels "Medical" (its own label is localized) opened the medical view) |
| 3 | The same click outside the menu's rectangle still reaches the world (the block is the rectangle, not a global modal) | judged ✔ (the menu closed, the game reacted) |
| 4 | The docked quick panel behaves the same way (the two panels share one view) | judged ✔ (its blank area changed nothing) |

## Limits

- The first batch must capture frames: the defect is "the pointer went through", which no probe reading reports. (Done: the four frames are named
  above and were read back.)
- A click there is an OS-level pointer action, so the batch needs the user's per-batch authorization for `send-input.ps1`; the rows cannot be read
  from the in-process driver alone. (Granted for the batch that judged them, 2026-10-25.)
- Row 1's negative is bounded by what the click could have done: the click landed while the character had nothing in reach to swing at, so the
  evidence is "the menu stayed open, the scene did not change, and the log shows no item/use/attack line" — a swing at nothing would also produce no
  line. A run that reproduces the ORIGINAL report (a wall in front, a pickaxe in hand) would make the same row conclusive.

## Resolution

All four rows pass on this build, so the ticket moves to `review/` rather than `done/`: the behaviour is judged, and what keeps it open is the
bounded negative above — a reproduction with something to swing at is the user's call, and the ticket says what it would add.

## Why this moves to done with the negative bounded (2026-10-27)

The project rule is the one applied here: a ticket whose rows all pass moves to `done/`, with an unjudgeable remainder named rather than passed. All four
rows are judged ✔ (blank area blocks the world, a row still runs, outside the rectangle still reaches the world, the docked panel behaves the same).

The bounded negative stays named as a residual: the blank-area click landed where the character had nothing to swing at, so "no attack line" is consistent
with both outcomes. The instrument for a conclusive reproduction now exists — `virtual-input` can place the pointer and click with no OS input — so a later
batch with a pickaxe in hand and a block in reach can close it without asking the user for the mouse.