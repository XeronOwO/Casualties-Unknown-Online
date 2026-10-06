# Cross-player use by drag: the gesture has to announce itself (highlight, label, cue sound)

- Status: Todo — **user finding (2026-10-06)**: asked whether the gesture gives any feedback, the user ruled
  that a rule with no UI and no sound reads as a feature that does not exist, and an occasional misfire reads
  as a bug; UI and sound cues are wanted.
- Priority: Medium
- Category: Player interaction / cross-player item use / feedback and discoverability
- Source: User message (2026-10-06), reviewing the landed cross-player use-by-drag gesture.
- Related: `todo/cross-player-drag-use-model-bounds.md` (the sibling finding: where the release counts and who
  it picks), `src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraDragUsePatch.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs`,
  `docs/evidence/selfchecks/items/cross-player-item-use-selfcheck.md` (historical: the Online UI's static
  "Use" / "Use with" buttons were removed on purpose, which is what left the gesture unannounced).

## What stands today (audited 2026-10-06)

1. **The gesture shows nothing at all.** No adapter code highlights a remote player, changes the cursor or
   plays a sound for this gesture: the only touch on the drag presentation is
   `PlayerCameraDragUsePatch.ClearDrag` clearing the drag image when a release is refused. A player who has
   never been told about the gesture sees a held item and no reason to drag it onto a teammate.
2. **Nothing announces eligibility either.** The Online UI's static "Use" / "Use with" buttons were deleted
   because they did not match the intended interaction, so the drag itself is the only entry point, and
   whether the held item is even usable on a person (`LocalUseItemEligibility`: wearables, known food,
   injectable/IV medicine, drinkable medicine, topicals, limb tools, any container whose liquids are all
   known) is invisible until the release either works or does nothing.
3. **A refusal is a log line only.** The host refuses with a named line (`[ItemUse] refused: {Target} is not
   conscious/alive and cannot receive a consumable.`, or the empty-catalog refusal) and the operator's screen
   shows the same nothing as a gesture that never landed — which is exactly the shape the user describes as
   "an occasional misfire reads as a bug". The design rule the rest of the family follows is "refused,
   observably", and today the observation is in a file, not on the screen.

## What a fix has to decide (user-visible, so the user's call)

1. **The hover cue.** When an eligible item is dragged and the cursor is over a valid player: an outline or
   tint on that player's limbs (a display-layer effect on the clone), a marker under the target (a ring on
   the ground, the way the game marks world targets), and/or a small label near the cursor ("release to
   use / 松开使用"). Recommended: the marker plus the label, because both are readable without touching the
   clone's own rendering, and the clone's sprites are also used by the backpack projection.
2. **When the cue appears.** For any dragged item, or only for the ones `LocalUseItemEligibility` accepts
   (recommended: only those, so the cue itself teaches what can be handed over) — and what an INELIGIBLE item
   dragged onto a player shows (recommended: no positive cue, plus one distinguishable refusal cue if the
   release still does nothing).
3. **The sound.** A short cue on hover-enter, on a successful use, and on a refusal. The repository's rule is
   to reuse what the game already has rather than shipping audio: the native UI click/confirm/deny clips
   (`PlayerCamera.PlayUISound`, the `switch` clip `Body.SwapSlots` plays) are the candidates; which one reads
   as "handed over" is the user's taste.
4. **The refusal must reach the operator.** Whether the host's refusal is echoed to the acting client as a cue
   (sound + label) — recommended yes, with the reason text kept to one localized line, because the operator
   otherwise cannot tell a refusal from a bug.
5. **Scope of the cue.** Whether the same treatment applies to the other cross-player gestures (the medical
   drag onto a limb, the give-an-item push of `todo/give-item-to-another-player.md`) or only to this one.

## Acceptance (the rows a run must read)

| # | Scenario | Expected |
|---|---|---|
| 1 | Drag an eligible item and move the cursor onto a teammate | A cue appears on that teammate (and disappears when the cursor leaves), with no change to the item or the target |
| 2 | Release it there | The use lands and the success cue plays on the operator's screen |
| 3 | Drag an INELIGIBLE item onto a teammate and release | No positive cue, no item movement, and one cue/line distinguishable from a successful use |
| 4 | Release on a target the host refuses (unconscious, empty container in the catalog) | The refusal reaches the operator's screen (cue and one line), not only the log |
| 5 | Drag an eligible item with the cursor on a teammate while two players overlap | The cue marks the same player the release picks (the sibling ticket's rule) |
| 6 | Solo / no session | Unchanged, no cue |

## Non-goals now

- Not the target geometry or the overlap rule: `todo/cross-player-drag-use-model-bounds.md` owns those, and
  the cue must follow whatever that ticket decides.
- Not a new wire message: the host's refusal reason already exists as a log line, and carrying it to the
  operator is a presentation decision (an existing result/refusal message if one fits, otherwise named as a
  wire change in the implementation cycle).
- Not new art or audio assets.
