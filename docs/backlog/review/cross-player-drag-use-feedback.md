# Cross-player use by drag: the gesture has to announce itself (highlight, label, cue sound)

- Status: Review — the cue's remaining half landed (2026-10-24, deployed `0.1.0+9510981e`): the REFUSAL now reads
  on the operator's own screen as a localized line plus the game's deny sound, and the user ruled there is NO extra
  success sound because the drag already has the game's own cloth rustle. Two runtime rows still need a real drag
  (mouse input the acceptance tooling does not drive) and are named at the end of this page.
- Priority: Medium
- Category: Player interaction / cross-player item use / feedback and discoverability
- Source: User message (2026-10-06), reviewing the landed cross-player use-by-drag gesture.
- Related: `review/cross-player-drag-use-model-bounds.md` (the sibling finding: where the release counts and who
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
   `done/give-item-to-another-player.md` — or only to this one. Recommended: apply the naming rule to every
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

- Not the target geometry or the overlap rule: `review/cross-player-drag-use-model-bounds.md` owns those, and
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
2026-10-23:** the USE moved from the range to the displayed backpack's CENTRE (`done/give-gesture-own-ui-and-center-use.md`),
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

## Batch `20261025-k` — the drag was attempted and did not start, with the reason named

The mouse capability the 2026-10-24 status said the tooling lacked now exists (`send-input.ps1 -Action rdrag`: a real
right-button press, a stepped pointer move and a release), so this batch tried row 1 on a live session:

- The ring was opened with `Tab` and `ring-buttons` reported six buttons, one of them holding the emergency light at
  `520,419` — an eligible item, so the drag had something to carry.
- The right-drag from that button to the teammate's projected point (`622,624`, the guest standing beside the host at
  `1,506`) was sent with a 6 s hold, and `drag-state` sampled mid-hold answered **`dragging=false, dragItem=none`**: the press
  never entered the drag bracket, so no hover cue could be read. The frame taken at that moment confirms it — the ring is drawn
  with its slots and no dragged item follows the pointer.
- What the earlier session already proved is the next step's starting point: a right-press at a **band** point of the ring
  (`514,460` then) did enter the bracket (`dragItem=bandage`), while a press on a **slot button** like `520,419` selects through
  the control instead. The next attempt therefore presses a band point, not a slot, with the same `rdrag` and reads the frame
  during the hold.

**The same batch then tried that band point and two others, and all three answered `dragging=false`** (`dragItem=none`,
`drag-state` sampled mid-hold, with the ring open and the emergency light in the ring's own button list): `520,419` (the slot's
own button), `717,317` and `514,460`. The difference from the earlier success is STATE, not the coordinate: the ring drawn in
these frames is the EQUIPMENT page (`主手/副手/上臂部/中臂部/下臂部`), while the earlier `dragItem=bandage` came from a page
whose slots held a carried item, and `ring-buttons` lists the emergency light as a slot of THIS page. So the drag row's missing
capability is now precise: the batch must put the cursor over the CARRIED item's own band (the inventory page's slot band), not
the equipment page's, before the right-press can enter `PlayerCamera`'s drag bracket. Parked there by the two-batch rule; the next
attempt starts from the right PAGE, not another coordinate.

**Batch `20261025-t` — the page hypothesis is dead too, and the finding is the input path itself.** The ring in this session
DOES list carried items on its own slots (`Untagged@520,568:0:funguschunk`, `Untagged@761,568:1:craftingbottle`), so the page
argument above was wrong: the item sits on the page the batch pressed. With the ring open, no modal window and no minigame, a
right-press on the **item's own slot centre** (`520,568`) → `drag-state` mid-hold answered **`dragging=false, dragItem=none`**
again. Combined with the earlier points (`520,419`, `717,317`, `514,460`) that is five presses, two pages and three states, and
the one success this session (`dragItem=bandage`) came from the same path — so the difference is not where we press but WHETHER
that press reaches `PlayerCamera` at all. The row now waits on an input path whose delivery is reliable (a window-station or
focus condition this shell does not control), not on a coordinate; the batch stops here rather than spending a sixth press.

**Batch `20261025-u` — the diagnosis, stated as the game's own fields see it.** During a right-button hold the pointer MOVES
(drag-state read `(640,299)` then `(583,229)` while the drag was in flight) but the game's button fields stay FALSE (`mouseHeld=false,
mouseDown=false` at +1.5 s and +2.7 s). Left clicks do reach it — an earlier click produced a real `OnBlockDamaged` trace — so the
split is precise: **cursor movement and left-button events arrive; the right-button press usually does not**, and the one
`dragItem=bandage` this session proved it can arrive. A future cycle therefore has a testable target instead of a theory
(activation/focus at the moment of the press, or a different injection point for the right button), and this batch records it there.

**Batch `20261025-v` — that target is now measured, and the answer is the ENVIRONMENT, not the gesture.** `drag-state` was
extended to read the bracket's own gates beside the button state, and every gate is OPEN while a press is held:
`allowUseItem=true, conscious=true, minigame=false, paused=false`, and the bind really is Mouse1 (`keybind-read action=iteminteract`
→ `keyCode 324, "Mouse1"`). What is false is the INPUT ITSELF, and not only for the right button: a LEFT-button hold sent through
the same path also read `mouseHeld=false, mouseDown=false` mid-hold. Cursor position arrives (the pointer moved through both
holds) and keys arrive (a real Tab press opened the ring), but **no mouse BUTTON event reaches Unity's `Input` on this station** —
which is UIPI-style blocking or a window/child-window condition, not a coordinate, a page, a state or a bind. Every row that needs
a clicked or dragged target waits on that one environment fact, and no amount of re-pressing will change it; the rows stay open
with this named rather than closed on a guess.

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

## The refusal half landed (2026-10-24, deployed `0.1.0+9510981e`), and the user's two rulings

Asked about the gesture's feedback, the user ruled on 2026-10-24:

1. **No extra success sound.** Dragging an item into a slot or a container already plays the game's own
   cloth-rustle-like cue; adding a second one "would feel out of place". The highlight and the name label remain the
   success cue.
2. **A refusal is a line plus the game's deny sound**, so the player both sees and hears why the gesture did nothing.

The refusal half is what the code was missing: a refused cross-player use was logged by the host and the operator's
screen showed nothing at all, which is indistinguishable from a gesture that silently did nothing. It now:

- carries the host's own reason over `NetMsg.PlayerItemUseRefused` = 165 (host → the operator, reliable);
- returns that reason from `PlayerItemUseExecution` (every refusal site states one, and the host's detailed log line
  stays where it was);
- publishes the localized line `hud.use_refused` on the operator's screen — the same path for the host's own
  gesture and for a member's — and fires the Game Adapter's `PlayerCamera.PlayUISound(Deny)` beside it.

**Rows still unread, named rather than claimed**: the highlight and the label appear on a remote body during a real
drag, and a refused drag shows the line and plays the deny sound in-game.

**Batch `20261025-ar` — the lever is NOT missing input, and this page's earlier "tooling does not drive it" claim was
wrong.** Two measured facts replaced it:

1. **The right button arrives.** With a 6 s hold in flight, the client's own read answered `rightHeld=True` on every sample
   (`Input.GetMouseButton(1)`), and a probe-driven press/release flipped it back to `False`. The earlier `mouseHeld=false`
   samples were taken while another window still had the foreground (`GetForegroundWindow` returned the other client's handle
   for the first ~1 s), so the press edge landed in the wrong window.
2. **The bracket was refusing because the OPERATOR WAS UNCONSCIOUS.** `HandleStartDragging`'s first line is
   `if (!this.body.conscious) return;`, and the live read at the time of the failing attempts was `conscious=False,
   allowUseItem=False`. After `/heal @s` the same in-process start answered **`dragItem=emergencylight(Clone)`**, and a release
   answered `had=emergencylight(Clone) → after=none`. That is the whole of the "unreadable" state: an unconscious operator.

**Two instruments now exist so the row does not depend on absolute OS coordinates at all**, which is the other lesson: the
inventory ring DRIFTS (its buttons moved from `515,290` to `526,272` between two reads) and the game's `Input.mousePosition`
is a bottom-left DPI-scaled space (`x_game ≈ 1.25·x_set`, `y_game ≈ 757 − 1.25·y_set` measured to 1 px), so a fixed OS click
chases a moving target. `tools/acceptance/recipes/drag-start.cs` runs the EventSystem's own raycast at a point and calls the
game's `HandleStartDragging` — the exact function `Input.GetKeyDown(iteminteract)` calls — and
`tools/acceptance/recipes/drag-release.cs` does the same for `HandleReleaseDragging`. Their cast list is the EventSystem's real
answer (each entry reported with its `InvButton`'s item and overlap verdict), not a fabricated struct.

What remains for this ticket's rows is therefore a TARGET, not a lever: releasing over another member's body needs that
member's on-screen point, and the projection of the placed guest answered `screenY=4100` with `screenHeight=720`, i.e. the
point was outside the camera's view. The next cycle puts the guest inside the host's view first (a placement the host is facing)
and then releases there.

**Batch `20261025-as` — the cross-player drag now runs end to end, and it took three corrections to get there.** With the guest
placed 8 units from the host and projected to `(638,523)` (bottom-left origin, so 163 px below the screen centre), the host
`drag-start` on its ring's bandage slot answered **`bandage(Clone)`**, the real cursor was moved to that projected point, and
`drag-release` answered **`had=bandage(Clone) → after=none` with casts `MainHead`** — the release landed on the other member's OWN
body part and the bandage left the operator's hand (the patient's limb reading moved from 100 to 89.337, i.e. the game ran its own
use on that limb). Three facts made the difference and are worth keeping:

1. **The CUO Online window blocks game input while it is open.** `state` answered `windowVisible=true` on page `Players`, and every
   cast list carried `CUO Online Input Blocker`; with it open, a Tab press reaches the window (`key-tap` → `posted=True,
   isForeground=True`) but never opens the ring. `click window.close` first, and the same Tab opens it.
2. **`InvButton.Overlaps` reads the REAL cursor**, not the point a probe asks about (`Vector2.Distance(radialMenu.position,
   Input.mousePosition)`), so an in-process start must be preceded by actually placing the cursor on the button — otherwise every
   entry answers `dragItem=none` while the raycast at that same point clearly finds the button.
3. **The ring's centre is the screen centre**, so a release within ~100 px of it hits `RadialCenter` and never reaches the world
   body; the target must be projected at least ~150 px off-centre, which is why the guest was placed below the host rather than in
   front of it.

The two rows this ticket still owes are now a reading away: the highlight and the label during such a drag, and the refusal path
with its line and deny sound.

**Batch `20261025-at` — the cue's own condition is now read from the code, and it needs the remote BACKPACK's centre, not the
wound view.** `IDragUseCueState` is `DragUseCueTracker`, whose `Update(Item)` shows the cue only while
`RemoteBackpackView.IsOpen && DragUseCenter.IsUnderPointer(PlayerCamera.main)`, and names `RemoteBackpackView.FocusedSteamId`. It
also says why: "the centre is the release's own use/wear target … the label and the release cannot disagree",
`DragGiveTargetResolver` adds that a release on a SLOT is a give and one on the centre is the use, and that the world fallback
cannot reach a clone at all because `CloneInventoryRenderer` disables every proxy collider. So the gesture is: open another
member's DISPLAYED BACKPACK, hold the item, put the pointer on that panel's centre, and read the same two facts the overlay draws.

Measured in that shape: with the remote wound view open (`remote-wound-special mode=open` → `opened=true,
displayBodyName=MedicalDisplay_111…` — which needed the operator ALIVE first, the log naming `local body is not alive/available`
when it was not), a real right-button hold on the ring's bandage (`rightHeld=True`, `dragItem=bandage`) and the pointer moved over
the displayed body, the new `recipes/drag-cue.cs` read **`showing=false, targetSteamId=0`** — correct, because the pointer was over
the wound view and not over a displayed backpack's centre. The next cycle opens the remote BACKPACK (not the wound view) and reads
the cue there.

**Batch `20261025-au` — two facts that unblock the shape, and one absence that is now the question.** First, the give/use path
silently requires the TARGET to be alive: the guest had gone `alive=False, conscious=False`, and `/heal <id>` did nothing for it
(the selector is not an id) while **`/heal @a`** revived both members and answered "Reset …: sent to that player's own client …
it was dead, so it revives". Second, the cross-player USE itself is proven to execute: `minigame-end` later found
**`before=BandageMinigame`** running on the operator's client — the release that landed on `MainHead` really did start the game's
own bandage minigame for the patient's limb, and nothing in the scripted path had asked for that.

What is still missing is the cue and the auto-open, and their code says why they can be absent: `CrossPlayerDragUse.Observe` picks
a target from the mouse's WORLD point against each remote's model box and logs `[DragUse] display target … picked among N`, while
`DragUseCueTracker` needs `RemoteBackpackView.IsOpen` and the pointer on `DragUseCenter`. With the guest revived and placed two
units away, the wound view closed with Escape (`ui-casts` fell from the body buttons to `RadialCenter`), a bandage held on a real
right-button press and the pointer moved onto the projected body, the cue still read `showing=false, targetSteamId=0` — and after
raising the log level with `log-level level=debug`, **neither `[DragUse]` nor `[BackpackView]` produced a single line**, which says
`Observe` is not being called during this scripted drag at all. That is the next question: which frame callback drives
`CrossPlayerDragUse.Update`, and whether a drag started by a press on the ring runs it.

**The gate that answers that question is in the class's own header**: "The give gesture's own-UI gate lives at the two patches
(`GiveGestureModePolicy`): **while the player's own ring or a panel is open, nothing here runs**." A drag that starts by pressing an
item on the open ring therefore runs with the ring open, which is exactly the state where the whole feature is switched off — and
`HandleStartDragging` does NOT close the ring itself (its tail only fills `dragImage`; the `radialOpen = false` in `PlayerCamera`
belongs to the Escape handler). Sending Escape after the item is picked does close it without dropping the item — measured:
`item='bandage', radial=False, rightHeld=True` — and the cue still read `showing=false`. So the ring gate is now satisfied and the
remaining question is the SELECTION: whether `TryModelHalfSize`/`DragTargetSelection.Consider` accepts the remote's model box at
the pointer, whose only diagnostic line is emitted on a target CHANGE (`[DragUse] display target … picked among N`), which also
never appears.
