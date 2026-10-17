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

1. **The cue must NAME the target — that is the requirement, not a decoration (user ruling 2026-10-06).**
   When an eligible item is dragged and the cursor is over a valid player, the operator must be able to read
   WHICH player the item is about to be applied to: the label carries that player's display name (the same
   name the Online UI member list shows, with the `player-{steamId}` fallback the backpack view already uses
   when a peer reports no name), so a release is never a guess. This is what makes an overlap case safe: two
   players standing on each other cannot be told apart by a highlight, but a named label says who will
   receive it before the operator lets go. The supporting cue (an outline/tint on that player's limbs, or a
   marker under them) may be added around the label, but it can never replace it. Recommended shape: the
   named label near the cursor plus a marker under the target, because neither touches the clone's own
   rendering, which the backpack projection also uses.
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
5. **Scope of the cue.** Whether the same treatment (including the named target) applies to the other
   cross-player gestures — the medical drag onto a limb, the give-an-item push of
   `todo/give-item-to-another-player.md` — or only to this one. Recommended: apply the naming rule to every
   cross-player gesture whose target can be ambiguous, since the ambiguity is a property of the scene rather
   than of this gesture; decision 184's disclosure rule is where the ruling is recorded.

## Acceptance (the rows a run must read)

| # | Scenario | Expected |
|---|---|---|
| 1 | Drag an eligible item and move the cursor onto a teammate | A cue appears naming THAT teammate (and disappears when the cursor leaves), with no change to the item or the target |
| 2 | Release it there | The use lands and the success cue plays on the operator's screen |
| 3 | Drag an INELIGIBLE item onto a teammate and release | No positive cue, no item movement, and one cue/line distinguishable from a successful use |
| 4 | Release on a target the host refuses (unconscious, empty container in the catalog) | The refusal reaches the operator's screen (cue and one line), not only the log |
| 5 | Drag an eligible item with the cursor on two players standing on each other | The label NAMES the player the release will pick, and the release picks that same player — the named cue and the sibling ticket's resolution rule agree |
| 6 | Drag an eligible item onto a teammate the session has no display name for | The label still names the target, with the `player-{steamId}` fallback |
| 7 | Solo / no session | Unchanged, no cue |

## Non-goals now

- Not the target geometry or the overlap rule: `todo/cross-player-drag-use-model-bounds.md` owns those, and
  the cue must follow whatever that ticket decides.
- Not a new wire message: the host's refusal reason already exists as a log line, and carrying it to the
  operator is a presentation decision (an existing result/refusal message if one fits, otherwise named as a
  wire change in the implementation cycle).
- Not new art or audio assets.
## The cue's rule landed (2026-10-18), and the reading that shaped it

`DragUseCue` and `DragUseCuePolicy` decide WHEN the gesture announces a target and WHOM it names: an item the
family gate accepts, dragged over a player the same nearest-player resolution the release uses, shows a label
carrying that player's steam id. Six cases pin it, including the two that keep the cue honest — an item the
release would drop shows nothing (so the cue teaches what can be handed over rather than promising a target it
will not use), and a row without an identity shows nothing (so the label cannot disagree with the release).

**Three things the reading settled, so the wiring does not have to re-derive them:**

- **The per-frame hook exists and is not the release patch.** `PlayerCamera.HandleReleaseDragging` runs only on
  release, so the cue cannot live there; `PlayerCamera.HandleWhileDragging` runs every frame of a drag and is
  already patched in this tree (`PlayerCameraHandleWhileDraggingPatch`, which owns the favourite-mark logic).
  The cue belongs in its OWN patch class on the same method rather than inside that one, which is already
  carrying a different concern.
- **The label's NAME is not decided here.** The resolution the ticket asks for — the Online UI's own display
  name with the `player-{steamId:X}` fallback — already exists on the presentation side
  (`OnlineUiContext.DisplayName`), next to `RemoteBackpackView`'s and `RemoteMedicalView`'s copies of the same
  fallback. So the cue carries the steam id and the overlay resolves the name; a name decided in the adapter
  would be a fourth answer to that question.
- **The target must be the release's own resolution.** `CrossPlayerDragUse` walks the remote players for the
  nearest inside `OverlapRadius`; the cue has to read that same walk, not a second one, or the label and the
  release can name different players — which is exactly the overlap case the ticket says the label exists for.

## Limits this stage leaves

- **Nothing is drawn yet.** The decision exists and is tested; the cross-layer wiring (a port the plugin reads
  each frame, the patch that fills it, and the overlay that draws the label) is the next step, and the label
  has no runtime reading until it lands.
- **The sound is not implemented**, nor the refusal reaching the operator's screen (the ticket's items 3 and
  4), nor the scope question of item 5 (whether the naming rule spreads to the medical drag and the give
  gesture). All three have the ticket's own recommendations recorded; none is decided here.
- **The acceptance rows are unrun.** Rows 1–7 need a session with a dragged item and two players, and row 5 in
  particular (two players standing on each other) is the one the naming rule exists for.
## The label is drawn (2026-10-18)

The cue decision now reaches the screen. `IDragUseCueState` is the port the plugin reads; the game adapter
fills it from `DragUseCueTracker`, which recomputes once per while-dragging frame in
`PlayerCameraDragUseCuePatch` — its OWN patch class on the same method the favourite-mark patch uses, because
that one already carries a different concern. `DragUseCueOverlay` draws `Use on <name>` beside the cursor,
inside the window edges, as a second IMGUI surface beside the start-gate panel.

The name is the session's own display name for that player, resolved by the overlay: the adapter carries the
steam id and the presentation side answers the name with the `player-{steamId:X}` fallback the backpack and
medical views already use, so there is no fourth answer to that question.

Two facts are now runtime-visible for the next run: the label is drawn LAST in the frame so a mod window cannot
hide it, and a null dragged item clears it, so the label cannot outlive the gesture.
## Live evidence for the wiring, and one unresolved log finding (2026-10-18)

A two-client batch ran on the deployed artifact `0.1.0+efa4c49d` (`.acceptance/20261018-b/run3.log`): host and
member both reached the world, and every read row of the batch passed. That is the row this ticket can claim
from a run today — **the drag cue's patch class, port, tracker and overlay load and a full session plays with
them installed** — and it is not the ticket's own rows, which need a drag the driver cannot produce.

**The deployment was checked rather than assumed**, because a type-resolution error appeared in the host log:
`CasualtiesUnknownOnline.GameAdapter.dll` carries `DragUseCueTracker`, `IDragUseCuePatchPort`, `DragUseCuePolicy`
and `PlayerCameraDragUseCuePatch`, the Runtime carries `IDragUseCueState` and the plugin carries
`DragUseCueOverlay`, so nothing the change added is missing from the deployed set.

**The unresolved finding.** The host log carries
`[Error :Casualties Unknown: Online] [CasualtiesUnknownOnline.Plugin] [Unity:Exception] TypeLoadException: Could not resolve type with token 0100002e …`
immediately after the acceptance driver connects to the REPL. Its stack is entirely third-party schema
generation — `MonoCustomAttrs.IsDefined` ← `RuntimePropertyInfo.IsDefined` ←
`DefaultContractResolver.GetSerializableMembers` ← `NJsonSchema.Generation.JsonSchemaGeneratorSettings` — with **no
CUO frame in it**, and it fires at plugin-load time, before any CUO session or drag exists. Both clients then
completed the session and answered every read.

What is NOT established: whether this predates this change. The log is overwritten per run, so the previous
artifact's log is gone, and the error names no CUO type. The decisive next check is to run one client with the
drag code reverted (or with the REPL absent) and compare, or to capture the schema subject by logging the type
HotRepl asks about — recorded here rather than assumed away, because "it loaded before this" is not evidence.