# The acceptance channel's hand-staged pointer is replaced by the virtual pointer

- Status: Todo — opened 2026-10-27 after the virtual pointer was verified live (user's direction: "if it succeeds, the old mouse-wrapper
  stand-ins can be removed").
- Priority: High — every hand-staged pointer is a place where the batch proves something the game never did.
- Category: Acceptance tooling / input

## Design

`docs/acceptance/workflow.md` carries the instrument's rule ("The pointer can be driven in-process (`virtual-input`)"), and the surface it serves is
the same one `docs/design/surfaces/drag-cue.md` describes: the centre guard and the release both read the pointer. This ticket removes the stand-ins that
rule makes unnecessary; no product behaviour changes.

## What the player (the batch author) does today, and what it costs

Several instruments fake the pointer instead of moving it: `remote-gesture`'s `cast=` / `moved=` arguments build a synthetic `uiCasts` list and move
`clickPos`, `DragUseCenter` carries a `StagedPointer` for the centre guard, and the drag patches carry their own staged windows. Each one proves a
behaviour under conditions the game does not produce: a synthetic cast list is not the `EventSystem`'s answer, and `clickPos` is not `Input.mousePosition`.

## Why it can go now

`GameAdapter/Acceptance/VirtualInput.cs` patches `UnityEngine.Input.mousePosition` (a managed getter on this build) and answers a staged screen position,
verified live on 2026-10-27:

| Step | Read |
|---|---|
| `virtual-input mode=install` | `installed: true`, `inputAnswers: 1634,-159` (the real pointer) |
| `mode=move x=640 y=360` | `pointer: 640,360`, `inputAnswers: 640,360` — the game's own read answers the staged value while the real pointer stayed at 1634,-159 |
| `mode=uninstall` | `installed: false`, `inputAnswers: 1709,54` — the real pointer again |

With that, a batch can put the REAL pointer where it wants and let the game's own `EventSystem`, `PlayerCamera` and UI code answer it — which is what the
hand-staged seams were standing in for.

## The work

1. Offer `virtual-input` staging to the drag/gesture recipes, so a release is aimed by the pointer rather than by a synthetic cast index.
2. Remove `DragUseCenter.StagedPointer` and its `stage-pointer` recipe once the gesture path reads the real pointer (the guard reads `Input.mousePosition`,
   which the virtual pointer now moves).
3. Remove the synthetic-`uiCasts` argument from `remote-gesture` (or keep it only as an explicitly named diagnostic), and re-judge at least one row per
   gesture family the shortcuts served: a give into a slot, the centre-use release, and the context menu's pointer rule.
4. Record the limits: mouse BUTTONS cannot be patched (`GetMouseButton*` are internal calls), so a click still needs the game's own managed call site or a
   batch-authorised OS input; and an inlined getter would answer the real pointer, which every mode reports so it can never pass silently.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | One gesture row is judged with the pointer staged and NO synthetic cast: the game's own `EventSystem` raycast picks the target |
| 2 | `DragUseCenter.StagedPointer` and `stage-pointer` are gone, and the centre-use row is judged with the virtual pointer instead |
| 3 | The recipes' help text no longer offers a synthetic cast, or names it as a diagnostic that is never a row's evidence |

## Limits

- Buttons are icall on this build; a click needs the game's own managed entry (or a batch-authorised OS input), and this ticket does not change that.
