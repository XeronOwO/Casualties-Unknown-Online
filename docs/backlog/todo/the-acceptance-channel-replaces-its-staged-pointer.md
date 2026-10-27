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
| `mode=press button=0`, six-method build (2026-10-27) | the game's OWN click path answers too: `keyHeld: true, keyDown: true, keyUp: false` for `Input.GetKey(KeyCode.Mouse0)` — which is what `KeyBinds.GetBind("attack")` = `323 / Mouse0` makes the game read — next to `held/down/up: true/true/false`; `mode=release` gives `keyUp: true` for its frame only |
| `mode=press button=0` (2026-10-27, native backend) | `held: true, down: true, up: false` in the press frame, with the real mouse NOT pressed; a later call reads `held: true, down: false, up: false`; `mode=release` reads `up: true` for its frame only |

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

- ~~Buttons are icall and cannot be patched~~ **WRONG, corrected 2026-10-27**: the three button methods ARE patched through HarmonyX's native detour
  backend (`buttonPatches: 3`), and a staged press answers `held/down/up` exactly as a real one would. Harmony and MonoMod load as `0Harmony 2.9.0.0`
  plus MonoMod assemblies from the BepInEx core. The batch's remaining work is the REAL behaviour rows below, not the button channel.

## The real drag bracket runs on a virtual press (2026-10-27, three clients in a world)

The game does not read clicks the way the first draft assumed: `KeyBinds.GetBind("iteminteract")` answers 324 (`Mouse1`) and `HandleDragging`
(`PlayerCamera.cs:1353-1364`) starts the drag on `Input.GetKeyDown(iteminteract)`, while `attack` is 323 (`Mouse0`) and only attacks. With that known:

| Step | Read |
|---|---|
| drop a plastic chunk at the player's feet, read its screen point | `item-world-read` → `screenX 655.878, screenY 356.088` |
| `virtual-input mode=move` there, then `mode=press button=1` | the seam: `held: true, keyHeld: true` |
| `drag-state` | **`dragging: true, dragType: "Item"`** — `HandleStartDragging` + `TryPickupFromWorld` took the chunk |
| `mode=release button=1` | `drag-state` → `dragging: false`, and the game''s own line `[DragFlow] release fell through to the WORLD path (no UI target hit)` |

So a VIRTUAL right press engages the game''s real drag bracket, moves an item under the game''s own pointer read, and its release runs the game''s own
release path — with the real mouse untouched. Two facts worth keeping: the drag/pickup button is `iteminteract` (Mouse1), and a press must be stamped for
the NEXT frame, because the eval can run after the game''s `Update` in its frame (a same-frame stamp was never seen).

**Still open**: one UI click (the Online UI is IMGUI and may not read the virtual pointer at all — to be measured, not assumed), and then the removals this
ticket exists for.

## The GAME''s own Unity UI reads the virtual pointer; CUO''s IMGUI panel does not (2026-10-27, same batch)

| What was aimed at | How | Read |
|---|---|---|
| a slot of the game''s own radial UI (the remote-backpack ring) | `remote-gesture mode=list` gave the button at `x=773, y=477`; `virtual-input mode=move` there, then `mode=press button=1` | `drag-state` → **`dragging: true, dragType: "Item"` with `mouseX 773, mouseY 477`** — `TryPickupFromUI`''s raycast found the button, and `mode=release` ended the drag (`dragging: false`) |
| CUO''s own Online UI panel | the window opened over a known button position; `virtual-input` staged the pointer there and pressed | **no `ControlInvoked` line at all** — the panel is IMGUI and reads `Event.current`, which comes from the engine''s event queue, not from `Input.mousePosition` |

So the channel''s UI half is exactly the GAME''s UI: a row that needs a real Unity-UI press (`TryPickupFromUI`, `TryPerformUIActions`) can now be driven with no OS input. CUO''s own panel keeps the driver''s control-id channel (`-Action click`), which is the better instrument for it anyway — a control id cannot drift with the layout.

That closes the two behaviour rows this ticket needed measured; what remains is the removals it exists for.