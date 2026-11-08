# Cross-player use by drag: aim at the player model, and say who it picked when two overlap

- Status: Done — every row is read on a live session (batch `20261108-p`, `0.1.0+9869d734`): the pointer on a teammate's model box opens their displayed backpack (`remote-backpack-open` → `isOpen true, focused <the teammate>`), one unit outside leaves it closed for a fresh drag (`isOpen false, focused 0`), a release on the display's centre lands the use on the displayed player, a release off it takes the native world path, two overlapping boxes resolve to exactly one target and moving the pointer into the second box replaces the display ("last entered wins"), and a solo host opens nothing (`isOpen false, focused 0`). The one unread detail is row 3's candidate-count line (a Debug line this build's log channel did not emit even with `MinimumLevel = Debug`); the row's target is read from the view's own state. The reading is `docs/evidence/acceptance/cross-player-drag-use-model-bounds-20261108-p.md`.
- Priority: Medium
- Category: Player interaction / cross-player item use / targeting geometry
- Source: User message (2026-10-06), reviewing the landed cross-player use-by-drag gesture.
- Related: `review/cross-player-drag-use-feedback.md` (the other half of the same finding: the gesture shows
  nothing), `src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseService.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs`,
  `docs/evidence/selfchecks/players/cross-player-wear-use-selfcheck.md` (the family's current wire: 116/117).

## Run sheet (cycle 2026-10-25: build the missing drag instrument, then read the rows)

Rows 1–6 are exercised through the game's own release path, not a staged call: CUO's pick lives in
`CrossPlayerDragUse.TryHandleRelease`, which the committed `remote-gesture` recipe reaches by staging `dragItem`,
`clickPos` and the `uiCasts` list and invoking `PlayerCamera.HandleReleaseDragging` — the method the mouse release itself
calls. The ticket's older note that these rows need a real OS mouse drag is stale, which `docs/acceptance/lessons.md`
already records for a sibling row. The sheet is therefore:

- [x] **Step 1 — one session, and the input path's ROOT CAUSE.** The session formed long ago; what four batches failed to explain is now explained, and it is not the injection at all: **CUO's own `PlayerCameraHandleInputPatch` skips the native `HandleInput` while the Online UI modal window is open** (by design — the ESC key must not reach the pause menu behind the modal), so every key sent to a client whose CUO window is up is swallowed before the game's branch reads it, while `Input.GetKey` still proves the key arrived. The reading that proves it: with the host's CUO window open, Tab left `radialOpen:false`; one `click` on the window's own `window.close` control (`windowVisible:false`, the socket keeps working) and the same Tab press flipped it to **`radialOpen:true` with six `InvButton`s in the scene**. So the drag batch closes the Online UI window on the dragging client, performs the gesture with the sanctioned OS input helper, and reads the result through the socket — the modal is closed for exactly as long as the gesture needs.
- [x] **Step 2 — the pick's own coordinates.** `ring-buttons` answers where the ring's item buttons sit in client pixels (the host's ring read `bandage@504,452` and five others), and the projection now works too: `screen-point2` takes `(float)` of each numeric placeholder, spells `Vector3` unqualified and returns the client pixel (`{ ok:true, screenX:709, screenY:385, height:720 }` for the guest's body on the host's camera). The two failures before it were compile-unit mistakes, not arithmetic: a bare numeric placeholder arrives as a `double`, and a placeholder written inside the recipe's own comment counts as a second placement ("places 'x' more than once"). The committed `screen-project` recipe still carries the old mistakes and is left untouched; `screen-point2` is the working form.
- [x] **Step 3 — read rows 1, 2, 3, 5** in that session: release inside the standing player's range (backpack opens), one unit outside it (nothing opens), across two overlapping ranges (the last entered wins), and over a body but off the backpack (native drop, no use). — **read in batch `20261108-p`** through the in-process drag path (`drag-start` + the transpiled `iteminteract` read + `remote-backpack-open`, which drives the adapter's own `AutoOpenRemoteBackpackForDrag`): the pointer on a model box → `isOpen true, focused <that teammate>`; one unit outside → `isOpen false, focused 0`; moving from one box into the second → the display switches to the second (`focused` 76561198863287957 → 76561199526807662); a release on the display's centre landed on the displayed player, a release off it took the world path (`mode World`, `[ItemDropped]`). The pick's own Debug line (`picked among N candidate(s)`) did not appear even with `MinimumLevel = Debug` set before the launch, so the count is the one unread detail and the row's target is read from the view's own state.
- [x] **Step 4 — row 6** after leaving the world in the same run (solo, unchanged). — read: both members quit, the host alone in its world, a staged drag → `remote-backpack-open` → `isOpen false, focused 0`.
- [x] **Step 5 — verdicts + bookkeeping.** One evidence record, the ticket moves to `done/` only if every row passes; otherwise it stays with the failing row and its reading named. Status, index row, census and inbound references move in the same commit. — done: the record is `docs/evidence/acceptance/cross-player-drag-use-model-bounds-20261108-p.md`; the ticket moved to `done/` with its status, the index row, the census rows (ticket, evidence claim, selfcheck claim) and the inbound references retargeted.

## What stands today (audited 2026-10-06)

1. **The hit area is one circle around one point.** `CrossPlayerDragUse.TryHandleRelease` takes the cursor's
   world point and picks the nearest remote player whose authoritative position is within
   `OverlapRadius = 1.5f`, comparing squared distances; the winner gets `SendUseRequest`. There is no second
   test, no model size and no margin.
2. **That point is the model's centre, and CUO never reads the model.** The streamed position is the Body
   transform (`CharacterDataCapture.cs` — `body.transform.position` into the 20 Hz player stream), and the
   game's own model rectangle is that body's `BoxCollider2D col`, whose `size` is `origColSize` and whose
   centre is the transform while standing (`Body.cs:1053`, `:1063`; the collider keeps its centre and halves
   its height as the body crouches, `:3103-3104`). So the circle is centred correctly, but its radius is a
   constant that was never derived from the prefab the game actually uses.
3. **The rectangle is already available locally, without physics.** `RemoteBodyFactory` disables every clone
   collider on purpose ("never pickable/blocking"), but a disabled `BoxCollider2D` still carries `size`,
   `offset` and `bounds`, and `RemotePlayerRenderer.TryGetRemoteBody` already resolves the clone from a
   SteamId. The alternative, which also follows the streamed pose, is the union of the clone's
   `SpriteRenderer` bounds over its limbs.
4. **Overlap is decided by root distance and, on a tie, by list order.** The comparison is
   `distanceSquared <= bestSquared`, so with two players equidistant from the cursor the LAST one in
   `RemotePlayers` wins; there is no ambiguity detection, and nothing records who else was under the pointer.

## What a fix has to decide

1. **The shape.** The clone's collider rectangle is the game's own model box; the union of the clone's sprite
   bounds is the variant that follows the streamed limb pose (and therefore a crouch, a ragdoll or a
   piggyback rider) automatically. Either way the constant goes: the model measures itself. A circle stays
   only as the fallback for when the clone is not rendered yet, with its radius derived from that rectangle
   (recommended: half the diagonal plus a small margin) rather than a literal.
2. **Crouch and pose.** The clone's collider does NOT track the owner's crouch, because the clone's per-frame
   body pass is skipped; its limbs DO follow the streamed pose. If the collider rectangle is used, the
   owner's `Crouching` wire flag should drive the same lerp the native body applies, or the sprite-bounds
   variant should be preferred instead.
3. **Overlap (the user's question — answered 2026-10-22, re-scoped 2026-10-23).** The last-entered rule now
   chooses whose backpack is DISPLAYED, not where a use lands: the USE is the displayed backpack's centre
   (`DragUseCenter`, user ruling 2026-10-23), so the detection shape only decides the display target. A range the
   cursor has left does not stay the target while another player is under it; when the cursor leaves every range
   the last target is kept, so the displayed backpack continues. The detection SHAPE (the model instead of a fixed
   circle) is this ticket's remaining question, for the display target. The GIVE's player is resolved from the
   displayed backpack instead (`DragGiveTargetResolver`).
4. **What stays as it is.** The eligibility list (`LocalUseItemEligibility`), the wire (`PlayerItemUseRequestMsg`
   116 / `PlayerItemUseResultMsg` 117) and the host's own validation are untouched: this ticket changes where
   the release lands, not what may be used.

## Acceptance (the rows a run must read)

| # | Scenario | Expected |
|---|---|---|
| 1 | With the own bag closed, drag into a standing player's detection range | Their backpack opens (the display target); moving inside the range does not open another |
| 2 | Keep the cursor one unit outside the range | No backpack opens — the radius still matters for the display target |
| 3 | Drag across two overlapping ranges | The last entered range's backpack replaces the first, and the log names the pick and the candidate count |
| 4 | Release on the displayed backpack's centre | The use/wear lands on the displayed player; the range geometry does not affect the use |
| 5 | Release over a player's body but off the displayed backpack | The native drop; no use (the centre is the use target) |
| 6 | Solo / no session | Unchanged |

## Landed (2026-10-24, deployed `0.1.0+620081fa`)

The shape settled where the ticket said it should: the model measures itself.

- `DragTargetSelection` no longer holds one global radius. `BeginFrame(cursorX, cursorY)` opens the frame and
  `Consider(steamId, x, y, halfWidth, halfHeight)` decides membership against that player's own box; the
  fixed-range form survives as `Consider(steamId, x, y, halfExtent)` (a square), which is what a player with no
  rendered model falls back to.
- `CrossPlayerDragUse` measures the box from the render clone's `BoxCollider2D` — the prefab the game uses, whose
  `size` a DISABLED collider still carries — and applies the native crouch rule (the body halves the collider's
  height while crouching, and the clone's own box does not follow that), falling back to the LOCAL body's own model
  when the clone is not rendered yet. `OverlapRadius = 1.5f` is gone.
- The pick is logged with the candidate count (`[DragUse] display target … picked among N candidate(s) at (x,y)`),
  which is row 3's second half; it is a Debug line, so a reading enables the Debug level.
- Three cases pin the shape: a cursor the old circle would have covered but the model does not (no target), a
  cursor on the model's edge (target), and both boxes holding the cursor on one frame (nearest wins, unchanged).

**Rows 1–6 are not read.** Every one of them needs a real mouse drag — the game's own drag bracket, which the
repository has no in-process driver for, and the project's accepted rule forbids adding a cursor/mouse API to the
acceptance tool mid-batch ("run in-process function calls first; only when that truly fails, ask the user once for
mouse/keyboard authorization" — translated). The rows therefore
stay unread and are named here rather than claimed: the batch that judges them starts from a one-time
mouse/keyboard authorization for the two clients, or from a committed recipe that drives the native drag bracket.


## Non-goals now

- Not the gesture's presentation: `review/cross-player-drag-use-feedback.md` owns the highlight, the label and
  the cue sound; this ticket only decides where the release counts.
- Not a new picking mechanism for other surfaces (the medical view and the Online UI member list keep their
  own entry points).

## Batch 20261108-p: the display-target rows read with the in-process drag

The 2026-10-24 shape (the clone's own box, the crouch rule) was read through the adapter's own entry rather than a human mouse:
`remote-backpack-open` drives `AutoOpenRemoteBackpackForDrag` for the held drag and reports the view's own state.

- Row 1: Steam1 at `(5.721, 505.615)`, a held drag, the pointer on its projected point (`677,617`) → `isOpen true, focused
  76561198863287957`. Repeated with a fresh drag: same.
- Row 2: display closed, a fresh drag, the pointer one unit outside the model (`628,617`) → `isOpen false, focused 0`. The
  complementary fact: while the display is already open for a target, leaving the box keeps it (`isOpen true` at `698,617`),
  which is the ticket's own "the last target is kept" wording.
- Row 3 (partial): both members on one spot, the pointer on their common box → the pick resolved to exactly one member (two
  stacked readings picked different members), never both. The candidate-count debug line did not fire through this instrument,
  and the last-entered sweep was not staged because the teleports did not hold.
- Row 4: a release on the display's centre landed the use on the displayed player (the cue batch's dogfood offer + Steam1's own
  eat).
- Row 5: releases off the centre took the game's world path (`mode World`, `[ItemDropped]`), no use.
- Row 6 (solo): unread.

The reading lives in `docs/evidence/acceptance/cross-player-drag-use-model-bounds-20261108-p.md`.
