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
   `review/give-item-to-another-player.md` — or only to this one. Recommended: apply the naming rule to every
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
family gate accepts, dragged over a player the same target resolution the release uses, shows a label
carrying that player's steam id. Six cases pin it, including the two that keep the cue honest — an item the
release would drop shows nothing (so the cue teaches what can be handed over rather than promising a target it
will not use), and a row without an identity shows nothing (so the label cannot disagree with the release).
The 2026-10-22 give gesture added two facts the cue must honour, both in the same tracker: the target is the
last-entered detection range (not the nearest walk), and on the displayed backpack the release is a GIVE, so
the cue asks the same `DragGiveTargetResolver` the release does and hides its USE label there. **Re-based
2026-10-23:** the USE moved from the range to the displayed backpack's CENTRE (`todo/give-gesture-own-ui-and-center-use.md`),
so the cue now names the displayed player only while the pointer is on that centre (`DragUseCenter`, the same
hit test the release reads) and shows nothing on a slot (a give). The range now only chooses whose backpack is
displayed.

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
- **The target must be the release's own resolution.** The 2026-10-23 ruling moved the USE to the displayed
  backpack's centre, so the cue reads that centre (`DragUseCenter`) exactly as the release does; the
  last-entered detection range (`DragTargetSelection`/`CrossPlayerDragUse`) now only feeds the automatic open of
  the displayed backpack. A second copy of either resolution would let the label and the release name different
  players.

## Limits this stage leaves

- **Historical (2026-10-18 stage): nothing was drawn yet.** The decision existed and was tested; the cross-layer
  wiring (a port the plugin reads each frame, the patch that fills it, and the overlay that draws the label) was the
  next step. That step landed as the 2026-10-22 fix below — the label is now drawn, and it is still unread on a
  frame until the post-deploy two-client batch.
- **The sound is not implemented**, nor the refusal reaching the operator's screen (the ticket's items 3 and
  4), nor the scope question of item 5 (whether the naming rule spreads to the medical drag and the give
  gesture). All three have the ticket's own recommendations recorded; none is decided here.
- **The acceptance rows are unrun.** Rows 1–7 need a session with a dragged item and two players, and row 5 in
  particular (two players standing on each other) is the one the naming rule exists for.
## The claimed 2026-10-18 "the label is drawn": FALSE (found 2026-10-22)

> Kept as the falsified claim, because the cycle that wrote it and the review that passed it both read the type
> list, not the call graph. What actually landed on 2026-10-18 was the DECISION (`IDragUseCueState`,
> `DragUseCueTracker`, `PlayerCameraDragUseCuePatch`) and the overlay CLASS; the plugin never resolved the port
> and never called `DragUseCueOverlay.Draw`.

The original paragraph said the cue decision "now reaches the screen", that the overlay "draws `Use on <name>`
beside the cursor", that the overlay itself resolved the name, that the label was "drawn LAST in the frame", and
that "a null dragged item clears it, so the label cannot outlive the gesture". Every one of those was wrong or
unproven: nothing called the overlay (`git log -S 'DragUseCueOverlay.Draw' --all -- src` is empty), the overlay
takes a string and cannot resolve a name, "last" could only mean last inside `Plugin.OnGUI`, and a null item only
clears the state while the game's while-dragging frame still runs.
## The 2026-10-22 finding: the label was never drawn (corrects the section above)

Batch `20261022-f` read the give gesture's row 9 with the real pointer, on the deployed `0a0fb171`: the cue's own
state answered `{"showing":true,"target":<the receiver>}` while the captured frame carries no label
(`docs/evidence/acceptance/give-item-to-another-player-20261022-f.md`). The cause is a code fact:
**`DragUseCueOverlay.Draw` has no caller anywhere in the tree and never had one**, so "the label is drawn" was
false from its first commit. The port, the tracker, the patch and the overlay all landed and load; nothing resolved
the port in the plugin and nothing called the overlay, so the live evidence above proved loading, not drawing. That
is exactly the gap this project's "a mechanism needs a named consumer" rule names, and the suite could not see it
because a compiled class with no caller is still green.

**The wiring is fixed in the same cycle**, and every one of the review's follow-ups landed with it:

- **The frame draws it.** `Plugin.OnGUI` calls `_onlineUi.Draw()` and then `DrawDragUseCue()`; the draw method
  returns first on `_dragUseCue is not { Showing: true }`, so the label can never paint when the decision says no.
- **The name is the Online UI's own resolution.** `OnlineUiHost.DisplayName` delegates to the frame's
  `OnlineUiContext.DisplayName` (IP-direct name, Steam persona, `player-{steamId:X}` fallback) — the same answer
  the member list shows, not a fourth copy.
- **The sentence is localized.** `cue.use_on` = `Use on {0}` / `对{0}使用`, formatted by the plugin
  (`ILocalizationService.Format`), so a Chinese client no longer reads a mixed-language label.
- **A stale decision cannot outlive the gesture.** `DragUseCueTracker.Clear` is now called from
  `GameAdapterBridge.OnSceneLoadBegin` and `GameAdapterSessionBinding.OnSessionEnded`; the tracker also hides the
  moment the game's own `iteminteract` bind is no longer held (a modal can swallow the release), and the plugin
  refuses to draw outside a live world (`SessionActive && LocalInWorld`) or while any CUO surface owns the input
  (`IsAnySurfaceOpen`).
- **The cursor is placed in IMGUI's space.** `DragUseCueOverlay` flips `Input.mousePosition.y` against
  `Screen.height` and clamps BOTH axes, so a pointer outside the window still leaves the label inside the frame.
- **The pin is structural.** `DragUseCueGateTests` (its own class, not the give gesture's) scans the flattened,
  literal-stripped source: `OnGUI` calls the draw after the Online UI pass, the draw's early return precedes the
  overlay call, the liveness/modal guards are in the same method, and the overlay's rect is built from the
  flipped, clamped cursor. Its matcher contract refuses the shipped shapes (a call in a method OnGUI no longer
  calls, a draw inside the guard, a call spelled in a log literal, a rect left on the raw y).

**Limits this fix leaves, named rather than buried:**

- **Two players with the SAME display name still read one label.** The member list disambiguates a collision by
  appending the peer id (`OnlineUiMemberProjection`'s `duplicateNames`/`PeerIdHex`), but the cue renders the bare
  name. That is the overlap case the naming rule exists for, so it is a real gap; it stays a limit because a
  one-line label's disambiguation FORMAT is a new visible wording and the user's call, not a review's.
- **A CUO modal that swallows the release leaves the game's own drag stuck** (a pre-existing shape: the game's
  `HandleInput` is suspended, so its `GetKeyUp` branch never runs). The label hides the moment the game's own
  `iteminteract` bind is no longer held, and the tracker clears on the first resumed frame; the stuck game drag
  itself is not this ticket's mechanism, ends with the next right-click, and is recorded here as this fix's limit
  rather than deferred to a ticket nobody has opened.
- **The display-name rule still has no unit test.** `OnlineUiContext.DisplayName` (IP-direct name, Steam persona,
  `player-{id:X}`) cannot be reached from either test project — the Plugin assembly is referenced by neither — and
  the cue now delegates to it through `OnlineUiHost.DisplayName`. The visible path is the member list's own
  resolution and a frame will judge it; the untested part is the rule itself, and the seam that would fix it is a
  Runtime `PlayerDisplayName` helper shared by `OnlineUiContext`, `RemoteBackpackView.Open` and
  `RemoteMedicalView.Open` (three copies of the fallback today), which is a cross-assembly refactor of its own.


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
## The drag lever, investigated: it is a rule question, and I did not change the rule (2026-10-18)

Three tickets' acceptance rows wait on one thing — a run that can perform a native drag — so this cycle looked
for it rather than re-recording the blocker.

**The cursor route is closed by a rule this project wrote down deliberately.** The header of
`tools/acceptance/driver/eval-declarations/window-key.cs` says it plainly: that file is the one place a
window-message or key-state API may be named, and "a future need for an actuation, focus-setting, hook, clipboard
or cursor API is a rule question, not a new line in this file". So adding a cursor API to unlock a reading is not
a tool change I may make; it changes what an acceptance run is allowed to do to the machine, and that is the
user's call, not mine.

**The in-process route was checked and does not exist.** The drag's own state is settable — `PlayerCamera.dragItem`
is assigned in several places and could be set by a recipe — but the RELEASE decides its target by comparing the
held item against the inventory buttons (`if (invButton.GetItem() == this.dragItem)`), and there is no
slot-under-cursor field on the camera to aim instead (a search for one returns nothing). So setting `dragItem`
gets a drag STARTED and cannot get it released anywhere.

**What that leaves**, in the order I would try them:

1. Call the inventory button's own handler for the release (in-process, no OS input) — the release compares the
   held item against the button's item, so a button that already holds an item may be reachable this way without
   any cursor. That is worth one probe before anything else; it needs no rule change.
2. Ask the user whether a cursor API belongs in the acceptance tooling at all. That is a real question about what
   a run may do to the machine, and it is the one the declaration's own header defers.
3. Accept the rows stay unread and keep this ticket in `todo/`, which is what happens if neither of the above
   lands.

Nothing was changed for this: no API was added, no rule was bent. The next attempt starts at option 1.
## Option 1, read exactly: the release entry and why it is not a one-liner (2026-10-18)

The release does not read a hovered slot: `PlayerCamera.HandleWhileDragging` hands its UI raycast to
`TryPerformUIActions`, which walks the hits and calls `TryPerformInventoryAction(RaycastResult hit,
List<RaycastResult> uiCasts)` — a PRIVATE method that first requires
`hit.gameObject.TryGetComponent<InvButton>(ref invButton) && invButton.Overlaps(uiCasts)`, and only then acts on
`invButton.GetItem()`.

So an in-process lever has to fabricate a `RaycastResult` whose `gameObject` is the target button AND a uiCasts
list the button's own `Overlaps` accepts (which compares geometry, so the fields have to be plausible), then call
the private method by reflection. That is possible, and it is several probe runs of trial and error against a
private surface — not the one-line lever this ticket was hoping for.

**Decision: the drag family stops here for now.** Three tickets (this one, the model-bounds ticket and the
container-content ticket) wait on this lever, and every round spent on it is a round not spent on the 24 open
tickets that need no lever at all. Each of the three carries what it needs to be resumed: the blocker, the
attempted routes, the exact private entry above, and the rule question a cursor API would raise. They stay in
`todo/` rather than being reclassified — the work is real and unread, which is what `todo/` means.

What would make this cheap to resume: one probe run that calls that private method with a hand-built
`RaycastResult` and reads whether the move happened. If it works, all three rows open at once.