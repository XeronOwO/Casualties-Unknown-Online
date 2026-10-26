# Right-clicking an item that overlaps a teammate opens the member menu as well as picking the item up

- Status: Review — implemented 2026-10-26; the three rows need a mouse on a live session — opened 2026-10-25 from the user's report (translated): "when the item overlaps the guest, right-clicking to pick the item up also
  opened the guest's right-click menu".
- Priority: High — one gesture fires two actions, and the menu then covers the world the player is working in.
- Category: Online UI / pointer priority
- Source: user report (2026-10-25), seen with an item dropped on the guest's position.

## Design

Read before the change: `docs/design/surfaces/member-menu.md` (the surface whose pointer rule is at stake) and, for the gesture order the game
itself uses, `docs/design/mechanics/remote-inventory-entries.md`'s drag-start row (`PlayerCamera.cs:1437` takes the `Item` layer first). The record
this ticket comes from is the user's report and the game's own ordering.

## What happens

The game's own right-click on an item is the drag start (`PlayerCamera.TryPickupFromWorld` takes the `Item` under the pointer first,
`PlayerCamera.cs:1437`). CUO's member menu opens from a right-click near a remote player's PROJECTED body position — it uses the authoritative
entity stream, not a physics hit, because the clones carry no colliders. With the item lying on the player, both entrances accept the same click:
the game starts dragging the item and CUO opens the menu.

## Why it is wrong

One gesture, one action. The player's pointer was on an ITEM, and the game's own order (`TryPickupFromWorld` checks the `Item` layer FIRST and
returns) is the order the player reads: the item wins. The menu is the answer to "I right-clicked a PERSON", and a person is only under the pointer
when no item is.

## What the fix must decide (by reading, before code)

1. Where the menu's click is tested today (the overlay's IMGUI pass, the entity-stream proximity, or the same `uiCasts`/physics list the drag uses),
   and whether it can ask the same question the game asks — "is there an `Item` under the pointer?" — before it opens.
2. Whether the two entrances are in the same frame pass at all: the drag start is `PlayerCamera.GetKeyDown` in its own Update, the menu is CUO's
   IMGUI event, and a fix has to work whichever runs first.
3. What the priority is when an item overlaps a BODY: the item wins (the game's own order); a body with no item keeps opening the menu.

## Read 2026-10-25 — the cause is one missing question, and the fix is one seam member

`OnlineUiPlayerContextMenu.HandleInput` (`src/CasualtiesUnknownOnline.Plugin/OnlineUiPlayerContextMenu.cs`) handles the right button and asks
exactly one thing: `TryFindRemoteCandidatesAt(mouse, ctx, out candidates)` — a SCREEN-PROXIMITY search over the projected remote bodies
(`RemoteTargetPicker.Find(screenTargets, mouse.x, mouse.y, radius: 48f)`). It never asks what the pointer is over, so an item lying on a teammate is
invisible to it and the menu opens beside the pickup the game starts in the same frame.

The question it must ask is the game's own (`PlayerCamera.TryPickupFromWorld`, `PlayerCamera.cs:1423-1441`):
`Physics2D.OverlapPoint(Camera.main.ScreenToWorldPoint(mouse), LayerMask.GetMask("Item"))` — and the Plugin cannot ask it, because it does not
reference Assembly-CSharp or the game types (`GameAssemblyReferenceGateTests` fails the build if that reference comes back). So the question belongs
on the adapter's patch seam: one new member on `IPatchBridge` (a bool "is a world item under the pointer", implemented in `GameAdapterBridge` with the
two Unity calls above), called by the menu before `TryFindRemoteCandidatesAt`. That keeps the rule in the layer that owns game knowledge and leaves the
Plugin asking a question instead of knowing the game.

Rows 2 and 3 of the acceptance table then stay as they are (a body with no item keeps opening the menu; empty ground keeps its today behaviour), and
the bridge's pinned member census gains the one name in the same change.

## Implemented 2026-10-26

The seam the read asked for exists as `IWorldItemPointerQuery` (Runtime, engine-free: two floats), implemented by `GameAdapter` with the game own query
(`Physics2D.OverlapPoint` on the `Item` layer, pointer converted from GUI space), registered in the adapter composition, wired into the Online UI context
as `WorldItemPointer`, and asked by `OnlineUiPlayerContextMenu` before it looks for a teammate. Deployed and verified against this tree build.

The rule is written back on `docs/design/surfaces/member-menu.md` and in the audit ledger (row 185).

## Acceptance rows

| Row | The check |
|---|---|
| 1 | Right-clicking an item lying on a teammate's position picks the item up and does NOT open the member menu (read from the item's drag start plus the absence of the menu in a frame) |
| 2 | Right-clicking the same teammate with no item under the pointer still opens the menu |
| 3 | Right-clicking empty ground still does whatever it does today (no regression in the no-target case) |

## Limits

- The report does not say whether the menu closed when the drag began; the first batch reads that too, because a menu that opens and immediately
  closes would be a different (and lesser) defect than one that stays.
