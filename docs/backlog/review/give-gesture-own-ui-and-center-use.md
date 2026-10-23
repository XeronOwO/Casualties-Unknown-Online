# The give gesture respects the player's own UI

- Status: Review — the independent review's blocker and majors are fixed in the same change; the machine rows
  below are owed.
- Priority: Medium-High
- Category: Cross-player gesture / UI precedence / native parity
- Blocked by: nothing external. The machine reading needs one two-client session.
- Source: The user's review of the give gesture's entry (2026-10-23): a drag inside the player's own backpack must
  never be judged against another player, the displayed backpack's centre is the use/wear target, and the detection
  range only chooses whose backpack is displayed.
- Related: `review/give-item-to-another-player.md` (the gesture's rows, re-based here),
  `todo/placement-failure-report-anchor.md` (the receive side; unaffected by this entry change),
  `todo/cross-player-drag-use-feedback.md` (the cue this ticket re-points at the centre), decisions 217/218 (the
  native intent, the viewer never mutates a proxy) and the 2026-10-22 selection ruling this refines.

## Problem (evidence)

The give's entry read a player's detection range every frame the game was dragging, with no regard for the player's
own UI: `PlayerCameraDragGiveOpenPatch` called `AutoOpenRemoteBackpackForDrag` on every while-dragging frame, and
`CrossPlayerDragUse.TryHandleRelease` sent the cross-player use whenever the cursor's world point was inside the
range. The displayed backpack is the SAME native ring re-anchored onto the remote clone (`InvButtonBodyPatch`), so a
player moving an item inside their own backpack, with a teammate's range under the cursor, had their ring silently
replaced by the teammate's and their release turned into a give or a use. Batch `20261023-e` produced exactly this
context on the real clients (the range auto-opened the receiver's backpack while the giver's own ring was open); the
batch `20261022-f` records read the same behaviour as the intended entry.

## The user's ruling (2026-10-23)

- Inside the player's own UI — own ring, own wound/medical panel, container panel, crafting panel, trade menu — a
  LOCAL-item drag means the LOCAL body only: no detection range is read, no other backpack opens, no cross-player
  give/use exists. "自己背包中拖动就应该是对自己的语义". (The TAKE direction — a proxy dragged from a displayed
  backpack, view closed, released into the own ring — is its own shipped path and is unchanged; see Limits.)
- Closing the own backpack during the drag enters the world. The game's own toggle is used; one press means one
  thing (closing the displayed backpack and opening the own one is two presses, never one combined action).
- The detection range's only job is choosing whose backpack is displayed. It stays armed while a displayed backpack
  is open, so dragging from B's range to C's range displays C's backpack (the last entered range wins).
- A release inside the range but outside the displayed backpack keeps the game's own drop semantics.
- The displayed backpack's CENTRE is the use/wear target: a usable item is used on the displayed player, a wearable
  is worn by them, and an item with no use/wear action does nothing (the item stays in the giver's hand).
- A use does not close the backpack: the vanilla single-player ring stays open after a centre action.
- No hint is added. The native drag can force the ring open when the cursor crosses a body collider, so the mode is
  read from the live state every frame, never inferred from the last key press.
- The range radius is unchanged (1.5 world units); the own-UI gate has priority over everything else.

## The input sequence (the player's own hands)

1. Open the own backpack with the game's own toggle (`toggleinventory`, default Tab). The own ring is the OwnUi
   context: nothing cross-player is read.
2. Hold the right button (`iteminteract`, Mouse1) on the item's slot: the native `HandleStartDragging` →
   `TryPickupFromUI` starts the drag.
3. Keep the button held and press Tab once, with the cursor off any body/limb collider: the own ring closes and the
   world context begins. The native while-dragging body re-forces the ring open if the cursor crosses a body or limb
   collider (`PlayerCamera`'s `Physics2D.OverlapPoint` line); the remote clone has none, the local body and corpses
   do — which is why the mode is read live.
4. Move within 1.5 world units of a teammate: their backpack opens by itself, the ring re-anchors on their clone and
   the context is the displayed backpack. The range stays armed here: moving into another player's range replaces
   the display.
5. Release on a slot of the displayed backpack (the native `TryPerformInventoryAction` counterpart): a give. Release
   on its centre (the native `TryPerformRadialAction` counterpart): use/wear on the displayed player. Release
   anywhere else (the native `TryPerformWorldActions` counterpart): the game's own drop.

## The model (three contexts)

| Context | How it is read | What a drag means |
|---|---|---|
| Own UI | the player's own ring open (`radialOpen && !RemoteBackpackView.IsOpen`) or the wound view / container / crafting / trade panel open | local only: the range is not read, no other backpack opens, no cross-player give/use |
| World | neither of the above and no displayed backpack | the detection range opens the backpack of the range the cursor enters |
| Displayed backpack | `RemoteBackpackView.IsOpen` | the range keeps switching the display (B → C); a release on a slot gives; on the centre uses/wears; anywhere else the native drop |

## What landed

- `GiveGestureMode` + `GiveGestureModePolicy`: the pure three-way decision and the live read (own ring, the four own
  panels, the remote view as the displayed backpack). Own UI wins over a displayed backpack; a displayed backpack
  wins over the world.
- `DragUseCenter`: the native centre guard's GEOMETRY — the ring finished opening (`inventoryUseLeniency`) and the
  pointer is inside the centre circle (distance to `radialCircle` over `uiScale` below its radius). The tagged
  Graphic cannot be the gate: CUO's remote view hides it (`AnchorRadialToFocus` disables `radialCircle`), so a
  raycast can never reach it while a displayed backpack is open.
- `PlayerCameraDragGiveOpenPatch`: the own-UI gate before `AutoOpenRemoteBackpackForDrag`; the range is not read at
  all in the own-UI context, and a drag that ENDS there still resets the drag selection (`AutoOpenFor(null)`).
- `PlayerCameraDragUsePatch`: the own-UI gate before any resolution; inside the displayed backpack, the give
  resolver first, then the centre; the centre release is consumed even when the use seam answers false, so the
  native centre can never use or wear the item on the giver; a centre release re-asserts the native ring, because
  the native body clears it when no UI graphic sits under the hidden centre.
- `CrossPlayerDragUse.TryUseOnDisplayed` replaces the range-based `TryHandleRelease`: the target is the displayed
  player, with no range check and no close; the view-only pack refuses a use/wear (the access contract); the dose
  measurement is unchanged.
- `DragUseCueTracker`: the cue names the displayed player only while the pointer is on the centre; its port no
  longer carries UI raycasts (the centre is geometric).
- Tests: `GiveGestureModeTests` (the four-case matrix); `GiveGestureGateTests` re-based — the own-UI gate order in
  both patches, the drag-end reset, the centre release consumed and kept open, the centre hit test not needing the
  hidden tagged graphic, the displayed target and view-only rule, the four own panels, and the cue/release sharing
  the centre.

## Independent review (2026-10-23) — every finding and what changed

The change was reviewed in a fresh context against the frozen tree before its commit. Every finding is recorded
here with what it changed; the blocker and the majors were fixed in the same change.

- **BLOCKER — the centre hit test was unreachable while the displayed backpack is open.** `DragUseCenter` required a
  UI raycast carrying the `RadialCenter` tag, but `RemoteBackpackView.AnchorRadialToFocus` disables
  `camera.radialCircle` (the tagged Graphic) for the whole time the view is open, and a disabled Graphic is not
  raycastable. The centre release therefore fell to the native drop, the cue never showed, and the "no-use item
  stays in hand" row was wrong (it dropped). **Fixed:** the hit test is the native guard's GEOMETRY (ring scale and
  pointer distance to the live centre circle) with no tag/cast dependency; the gate pins that it does not read the
  tag.
- **MAJOR — the finaliser could not keep the ring open after a centre release.** The prefix clears the drag, the
  native "release with no drag" branch clears `radialOpen` unless a UI graphic is under the pointer, and the centre
  graphic is hidden — so the view closed before the finaliser's keep rule could matter. **Fixed:** the finaliser
  re-asserts `__instance.radialOpen = true` for a centre release (vanilla parity), pinned by shape.
- **MAJOR — the own-UI gate skipped the drag-end bookkeeping.** With the cue no longer calling `Observe`, a drag
  ending in the own UI never reached `EndDrag`, so `_dragActive`/`_openedFor` (and `ClearAutoOpened`) leaked into the
  next gesture and could silently refuse to open the same player's backpack again. **Fixed:** the open patch calls
  `AutoOpenRemoteBackpackForDrag(null)` for a null drag even in the own-UI context; pinned by shape.
- **MAJOR — the "no cross-player release" wording over-reached.** The gate sits inside the local-item branch; the
  TAKE direction (a proxy dragged from a displayed backpack, view closed, released into the own ring) is the shipped
  cross-player transfer and is unchanged. **Fixed in scope:** the ruling and the gate comment are scoped to a
  LOCAL-item drag; the take direction is recorded as unchanged (Limits).
- **MAJOR — live documents still stated the removed model.** The architecture page's local-item bullet and the give
  ticket's close-rule/cue bullets still described the in-range use and the range cue; the two live `todo/` tickets
  (`cross-player-drag-use-feedback`, `cross-player-drag-use-model-bounds`) still presented the range as the USE
  model. **Fixed:** all four are re-pointed at the centre/display model in this change.
- **MINOR — view-only packs bypassed the access contract for the centre use.** The access rule refuses every
  operation (take, move, drop, use, give), but the use request path had no check. **Fixed:**
  `TryUseOnDisplayed` refuses a view-only pack with one line; the release is consumed and nothing moves.
- **MINOR — unpinned claims.** `AutoOpenFor`'s range guard, `TryUseOnDisplayed`'s displayed target and view-only
  rule, the four own panels, and the hidden-tag hit test had no pins. **Fixed:** `GiveGestureGateTests` pins each
  of them by shape (the Unity reads cannot be behaviour-tested without a camera).
- **MINOR — stale comments.** `CrossPlayerDragUse.IsTargetInRange` still called itself the USE condition, and the
  `Observe`/cue docs still said the cue read the range. **Fixed** in the same change.
- **MINOR — the checklist's family audit said the remote medical view is not own UI.** It is (the remote medical
  focus IS the native wound view); the behaviour is benign because that family has its own patch. **Fixed:** the
  checklist says so.
- **MINOR — the two-Tab ordering residual.** A second Tab on the very next frame can re-satisfy `IsOpen` before the
  focus-clearing path runs, re-displaying the remote view for a frame. **Recorded** as a limit; not fixed.
- **MINOR — a dual-flag (wearable+usable) item.** The native centre runs both wear and use; CUO sends one family
  (wear first). **Recorded** as a limit to confirm against the ruling.
- **NITS taken:** the null-camera default is now the safe OwnUi; the own-ring read is documented; the give ticket's
  2026-10-22 finding keeps its own history and names the 2026-10-23 re-base separately; the give/centre priority is
  geometrically disjoint (centre radius vs the InvButton band) and recorded as such; the centre hit test no longer
  pretends to mirror the tag.

## Acceptance (machine rows, owed)

1. Own ring open, drag an item and move the cursor through a teammate's range: the teammate's backpack stays closed
   and the release keeps the local/drop action.
2. Close the own ring during the drag (the game's own toggle): entering a teammate's range opens their backpack;
   releasing on a slot gives; on the centre uses; in the range off the UI drops.
3. Centre use of a usable item on the displayed player: the target executes it, and the backpack stays open.
4. Centre wear of a wearable: the displayed player wears it, and the giver's body does not.
5. Centre with an item that has no use/wear action: nothing happens and the item stays in the giver's hand.
6. A release on the displayed backpack's centre is a USE/WEAR, never the native drop (`[DragUse] used …`), and the
   use cue names the displayed player there.
7. View-only displayed backpack: a centre use/wear is refused with one line and nothing moves.
8. B → C: with B's backpack displayed, dragging into C's range displays C's backpack (the last entered range wins).
9. The local wound/medical panel open: dragging an item does not open any remote backpack.
10. Repeated toggles: one press closes the displayed backpack, a second opens the player's own; no stuck state.

## Limits (recorded, not claimed away)

- The remote wear rides the existing `PlayerItemUseRequest`/`PlayerItemUseResult` path (the host places the wearable);
  this ticket adds no wire message and no new report.
- The detection range radius is unchanged; only which contexts read it changed. The range-edge release keeps the
  native drop (the user's ruling).
- The TAKE direction (a proxy dragged from a displayed backpack, view closed, released into the own ring) is the
  shipped cross-player transfer and is unchanged: it is not a local-item drag, so the own-UI gate does not cover it.
- No hint was added: the discoverable step is closing the player's own bag (the same key that opened it).
- A second Tab on the very next frame after closing a displayed backpack can re-display it for one frame before the
  focus-clearing path runs (ordering residual).
- A dual-flag wearable+usable item is worn, not used, because CUO sends one family and the classifier puts wear
  first; the native centre would run both. Confirm against the ruling.
- The machine rows are OWED: the round that landed this code was scoped to tickets and code only (the user's
  2026-10-23 instruction), and the machine was in a co-op session. Nothing here is machine-verified.
