# Cross-player use by drag: aim at the player model, and say who it picked when two overlap

- Status: Todo — **user finding (2026-10-06)**: told that the release is judged inside a 1.5-unit circle
  around the target's position, the user judged the area too small — visually it should be the player model's
  rectangle, a circle being an acceptable approximation only with a suitable radius — and asked what happens
  when two players overlap.
- Priority: Medium
- Category: Player interaction / cross-player item use / targeting geometry
- Source: User message (2026-10-06), reviewing the landed cross-player use-by-drag gesture.
- Related: `todo/cross-player-drag-use-feedback.md` (the other half of the same finding: the gesture shows
  nothing), `src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseService.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs`,
  `docs/evidence/selfchecks/players/cross-player-wear-use-selfcheck.md` (the family's current wire: 116/117).

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
3. **Overlap (the user's question).** Recommended rule: *what you see is what you get* — the pointer must be
   inside the target's rectangle, and when it is inside two rectangles the player drawn ON TOP under the
   pointer wins (the highest sprite sorting order, which is what the eye reads as "in front"); the chosen
   one and the number of candidates go into the log line, so an unexpected pick is a readable record rather
   than a mystery. The alternative — refuse when two players are under the pointer, and say so — is a
   deliberate choice and would have to be taken on purpose; what must NOT survive is the current tie-break
   by list order, which no player can predict.
4. **What stays as it is.** The eligibility list (`LocalUseItemEligibility`), the wire (`PlayerItemUseRequestMsg`
   116 / `PlayerItemUseResultMsg` 117) and the host's own validation are untouched: this ticket changes where
   the release lands, not what may be used.

## Acceptance (the rows a run must read)

| # | Scenario | Expected |
|---|---|---|
| 1 | Release an eligible item over the HEAD and over the FEET of a standing player | The use lands from both, i.e. the whole model rectangle counts, not a ring around its centre |
| 2 | Release it one unit to the side of the model, and inside the old 1.5-unit circle | Nothing happens (no accidental use) — the fix must match the model, not simply enlarge the circle |
| 3 | Release it over two overlapping players | The one visually in front receives the use, and the log names the pick and the candidate count |
| 4 | Release it over a crouching owner | The judged area follows the crouch |
| 5 | Release it over the dragger's own body | Unchanged: the native local behaviour (the self-use path is not this gesture) |
| 6 | Solo / no session | Unchanged |

## Non-goals now

- Not the gesture's presentation: `todo/cross-player-drag-use-feedback.md` owns the highlight, the label and
  the cue sound; this ticket only decides where the release counts.
- Not a new picking mechanism for other surfaces (the medical view and the Online UI member list keep their
  own entry points).
