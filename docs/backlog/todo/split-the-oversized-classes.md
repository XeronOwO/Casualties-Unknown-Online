# The recorded over-line classes still owe their splits

- Status: Todo
- Priority: High
- Category: Structure / maintainability
- Source: the delivery checklist's structure rule, whose recorded-ceiling path now requires an owning ticket
  (`SourceShapeGateTests.EveryRecordedCeiling_NamesAnOpenTicketThatOwesTheSplit`).

## Problem

Ten classes sit over the 600-line structure line with a recorded ceiling in `docs/architecture-debt.json`. That file is a RATCHET — a recorded
number may never be exceeded, and the gate fails on a class that grows past its record — but until this ticket it was also a permanent badge: no
entry said who owed the split, so a recorded class could sit over the line forever and nothing would ever ask for the extraction.

The gate now refuses a recorded ceiling that does not name an open ticket. This is that ticket, and every entry in the debt file names it. The
per-entry notes carry each class's own extraction plan; this page is the queue and the rule.

## Design

Structure rather than player-facing behaviour, so no mechanic page applies: the record this ticket comes from is the design audit
(`docs/design/audit/README.md`, the structure rows) and the delivery checklist's structure box, whose recorded-ceiling path is what makes this
ticket the owner of those ceilings.
## What the fix looks like

For each class, in the order that keeps the tree compiling between commits:

1. Split along the seam the entry's note already names, with the same behaviour and the same log lines.
2. Let the split LOWER the recorded number in the same change, or delete the entry when the class lands under 600.
3. Never raise a recorded number to accommodate growth: a class that grew needs its own extraction first, and a number that has to change upward is
   the signal that the split is now overdue rather than optional.

## The queue, measured 2026-10-25

| Class | Lines | The extraction its note names |
|---|---|---|
| `GameAdapter.Run.RunCoordinator` | 922 | `HostDescentFollow` (the descent phase machine), the local-body publish path into `LocalBodyStatePublisher`, then the bind/unbind jump table |
| `GameAdapter.Character.CharacterDataSync` | 738 | the injury trigger into `LocalInjuryReporter` (baseline, host-row absorption, the report call) |
| `GameAdapter.GameAdapter` | 667 | the frame's aggregate: split by domain tick, following `GameAdapterDomains` |
| `GameAdapter.GameAdapterBridge` | 663 | the patch bridge's report halves, one per family |
| `Runtime.Session.World.WorldService` | 640 | the read model from the mutation surface |
| `Runtime.Session.CharacterData.CharacterDataStore` | 629 | the store's merge/save halves |
| `Runtime.Session.PlayerInteraction.PlayerInventoryTakeService` | 626 | the take ladder from the intent handling |
| `Runtime.Session.World.IWorldControl` | 615 | the interface's read group from its command group |
| `GameAdapter.OnlineUi.OnlineUiWorldOverlayView` | 609 | the page rendering from the view's state |
| `GameAdapter.IPatchBridge` | 608 | the bridge's per-family interfaces |

## Acceptance rows

| Row | The check |
|---|---|
| 1 | Every class in the queue is under 600 aggregate lines, or its entry names this ticket and no higher number |
| 2 | Each split changes no behaviour: the suites pass unchanged and the log lines a run depends on are identical |
| 3 | The gate passes with the debt file smaller than it is today (entries deleted, none raised) |
## Split 1: the console's report half (2026-10-31)

`Runtime.Session.Commands.CommandConsoleService` was not in the queue above when the queue was measured; it grew past 600 with the console contract's four stages (the relay,
the line sink, the cheat family's gate and its refusals) and stood at 684 with a debt entry. The extraction its entry named was the reporting family, and that is what moved:
**`ConsoleReportWriter`** owns the ten subscriptions (cut, restore, live write, starting supplies, the write verb's late refusal, the give/summon/heal/gamerule answers, the
applied reset) and the lines they print, including the parts-to-lines rule for a host answer. The console keeps the editor, the panel and the dispatch, and is now **556 lines**.

Behaviour is unchanged: the writer prints through the same `IConsoleLineSink` the console already implemented, and the suites pass unchanged. Two gates had to follow the moved code,
which is the point of them: the sync-coverage evidence entries that quoted the old subscriptions now quote the writer's, and the sweep's file count for the `Commands` folder went
from 77 to 78. The debt entry is DELETED rather than re-recorded (the class is under the line), so the file is smaller than it was.
## Split 2: the overlay view's report-once family (2026-10-31)

`GameAdapter.OnlineUi.OnlineUiWorldOverlayView` was 609 lines with a debt entry that owed the LABEL POOL. The smallest honest extraction was a differentone the class
actually had: three methods that each existed only to wrap a logger behind a per-surface key (the missing camera, the refused projection, the pinned label) plus the key set they
shared. That family is `ReportOnce` now — a tiny type any overlay surface can hold — and the view is **587 lines**.

The label pool the entry named stays available as a later split if the view grows again; `ReportOnce` was the extraction that cleared the line without touching a rendering rule,
which is what the acceptance rows for the spectator strip depend on. The debt entry is DELETED (entries are 12 now, down from 14 at the start of this queue's work), and the folder
count follows the new file.

## Split 3: the world control's social half (2026-10-31)

`Runtime.Session.World.IWorldControl` was 615 lines: the whole world surface — generation, blocks, items, entities, weather, radiation, chat, spectator, pings — in one
interface. The three things one player says to another came out first because they are the one group whose members are not facts about the world at all: a text-chat line, a
watch notice and a transient location ping, each a report/relay pair with its own receive event. They are `IWorldSocialControl` now, and `IWorldControl` inherits it, so every
existing reader compiles unchanged while a reader that only needs the social surface can take the narrow one.

`IWorldControl` is **577 lines** and its debt entry is deleted (11 entries now). `WorldService` implements the members unchanged — the compiler proves that, because the
inheritance keeps them part of `IWorldControl` for every caller that already holds it. The folder count follows the new file.

## Split 4: the take ladder's destination rules (2026-10-31)

`Runtime.Session.PlayerInteraction.PlayerInventoryTakeService` was 629 lines, and the entry named the split it owed: the destination resolution — which slot a
custody transfer lands in, the first-free substitution with its log line, the exact-slot rule the give path refuses on, the container candidate order, the by-id lookup and
the one-line destination description. Those five rules are `ItemDestinationResolver` now; the service is **511 lines** and its debt entry is deleted (10 entries left).

The resolver is the shape the entry asked for: the rules that change for their own reasons (a slot rule, an ordering, a substitution policy) in one object both callers
share, so the give path and the take path cannot drift about where an item lands. `PlayerInventoryTakeService` keeps the ladder, the kernel bookkeeping and the
publication; the two `ResolveDestinationSlot` call sites now ask `_destinations`, the four static rules are addressed through the type.

**What is left in the queue**: `RunCoordinator` (922), `CharacterDataSync` (738), `GameAdapter` (667), `GameAdapterBridge` (663), `WorldService` (640) and `IPatchBridge`
(608, the frozen patch aggregate whose own doc names the per-domain port migration as the way it shrinks).

## Split 5: the character store's four duplicated visual senders (2026-10-31)

`Runtime.Session.CharacterData.CharacterDataStore` was 628 lines, and the block worth moving was not a subsystem but a FOURFOLD COPY: `SendCharacterSound`,
`SendCharacterAttackAnim`, `SendCharacterLandingVisual` and `SendCharacterRagdoll` each carried the same eight-line policy — nothing without an active session, a guest
reports to the host, the host broadcasts to every handshaken member, reliable — with only the message type differing. A policy change had to be made four times and
could drift between them.

That policy is `CharacterVisualPublisher.Publish(NetMsg, payload)` now, and each store method is a one-line expression. The store is **568 lines** and its debt entry is
deleted (9 entries left). The four sync-coverage evidence entries that quoted the old send bodies point at the publisher's own line, and the folder count follows the
new file.

## Split 6: the patch aggregate's world-generation hooks (2026-10-31)

`GameAdapter.IPatchBridge` was 608 lines and FROZEN by its own doc: no member may be added, and new patch work declares a per-domain port instead. The next domain to
take that route was the world-generation family — the generation-pass hooks (structure wrap, ore, liquid, building, item distribution) and the cell-keyed block writes
(set, damaged, custom-tile break). They are `IWorldGenPatchBridge` now, reached through `PatchBridge.WorldGen`, and the aggregate neither declares nor inherits them:
**548 lines**, debt entry deleted (8 entries left).

This is the migration path the aggregate's doc names, applied once more: the six patches that call those hooks now state the domain they depend on and cannot reach the
rest of the bridge through that reference. The shape gate followed the change — a ninth seam entry, `WorldGen` in the static seam's census, `IWorldGenPatchBridge` in
the port list, the nine names out of the frozen aggregate census, and the aggregate floor from 90 to 81 — and the damage-block gate reads its declaration in the port
that now carries it.

**What is left in the queue**: `RunCoordinator` (922), `CharacterDataSync` (620), `GameAdapter` (667), `GameAdapterBridge` (663) and `WorldService` (640).

## Split 7, partial: the owner-state event leaves the character sync (2026-10-31)

`GameAdapter.Character.CharacterDataSync` was 738 lines. Its entry named the injury trigger as one cut; the cut that clears the most is the whole OWNER-STATE EVENT: the
limb-latch report, the injury-rise trigger with its three pieces of baseline state (the watched body, the last posted bleed set, and the host row's apply counter), and
the full post-event payload both send.

They are `OwnerStateEventReporter` now, and the sync asks it whether a rise happened before it reports — `OwnerState.TrackInjury(localBody, _vitals.AppliedCount)` —
so the authority-absorption rule (the host's own push is never an injury) lives next to the baseline it reads. The sync is **620 lines**, so its entry is RE-RECORDED at
that number rather than deleted: the class is not under the line yet, and the queue's row 1 allows an entry that names this ticket and no higher number. The injury pin
followed the trigger into the reporter and still asserts both halves (the rise test and the payload).

**What is left in the queue**: `RunCoordinator` (922), `GameAdapter` (712), `GameAdapterBridge` (649), `WorldService` (640) and `CharacterDataSync` (620, this ticket).

## Split 8: the local heal-item query leaves the apply path (2026-10-31)

`GameAdapter.PlayerInteractionApply` was one line over the ceiling (601). The cut is the two questions the Online UI asks about the local body's heal items — whether it
carries one, and which instance ids it can offer — which shared one subtree scan and one exclusion rule (skip anything under a render clone: that is another player's
presentation, not this body's items). They are `LocalHealItems` now (a static helper with the scan in one place), and the apply path keeps two expression-bodied
forwarders. The class is **545 lines** and its entry is DELETED.

## The debt file after the sweep (2026-10-31)

Seven entries remain, each one measured against the gate's own count and re-recorded where it had drifted: `RunCoordinator` 922, `GameAdapter` 712, `GameAdapterBridge`
649 (was 663 before the world-generation port took its nine members), `WorldService` 640, `RemoteIntentApplier` 621 (was 684), `CharacterDataSync` 620 and `ExampleMod` 608.
`PlayerInteractionApply` and `PlayerInventoryTakeService` are gone from the file entirely.

**The gate counts a file's lines once per type it declares**, so a class with nested types counts higher than its raw line count; every number above is the gate's.

## Split 9, partial: the local body's state stream leaves the run coordinator (2026-10-31)

`GameAdapter.Run.RunCoordinator` was 922 lines and its entry names three cuts; the one with no phase-machine coupling is the middle one, the local-body publish path. It is
one method whose every rule is about the GAME's body behaviour rather than about the run — the ragdoll torso anchor with the carried rider's deliberate exception, the look
target and its timed override, the pose flags (sitting, sleeping, climbing, working out, the nap variant, dog shake, wall slide, exact limb poses) and the vitals-model
inputs the host reads.

It is `LocalBodyStatePublisher.Publish(Body)` now. The coordinator is **832 lines**; its entry is re-recorded at that number rather than deleted, because the phase machine
and the bind/unbind jump table are still in it and this ticket owes both. Three pins followed the moved code: the carried-pose pin (which file it reads the publish path
from), the vitals pin (the three model inputs' call site) and one sync-coverage entry (the exact-limb-pose capture). The state stream itself is unchanged — the publish
call carries the same arguments in the same order.

**What is left in the queue**: `RunCoordinator` (832, this ticket — the descent phase machine and the bind/unbind table), `GameAdapter` (712), `GameAdapterBridge` (649),
`RemoteIntentApplier` (621), `CharacterDataSync` (620) and `ExampleMod` (608).

## Split 10: the world service's report-recovery surface (2026-10-31)

`Runtime.Session.World.WorldService` counted **640** because it was split across two files: the service itself plus a partial that carried the guest report-recovery
surface "split out at the 600-line gate" by forwarding every call to `BlockReportChannel`. A partial exists only to satisfy the gate, and it kept the world control
declaring nine members whose state lives somewhere else.

The channel declares them now: `IWorldBlockReportControl` (block state W1, partial damage W2, break drops W1's drop half) is implemented by `BlockReportChannel`, and
`IWorldControl` exposes it as `BlockReports` — one door to the object that owns the tables. The partial file is DELETED, the service is a plain class again at **589 lines**,
and its debt entry is gone. The call sites moved with it: four adapter files and four test files now read `world.BlockReports.X`, three sync-coverage entries follow their
quotes, and the "half-open" seam is gone as well.

**What is left in the queue**: `RunCoordinator` (832, this ticket), `GameAdapter` (712), `GameAdapterBridge` (649), `RemoteIntentApplier` (621) and `ExampleMod` (608).

## Split 11: the character snapshot's wire half (2026-10-31)

`GameAdapter.Character.CharacterDataSync` was 620 lines, twenty over the ceiling. The cut is the SEND side of a snapshot rather than the capture: whether it goes as a
guest report or a host broadcast, whether it logs (only the 1 Hz report does, so a burst of inventory moves cannot flood the log), the missing-native-fields warning
and the layer stamp every snapshot carries so a receiver in another layer refuses it.

That is `CharacterReportSender` now: `Send(data, throttled, nativeFailure)` and `Stamp(data)`. The sync is **574 lines** and its debt entry is deleted (five left). The
capture stays where the body is — the sender never reads a body, it only decides how one snapshot travels. Two sync-coverage quotes followed their call sites and the
folder count follows the new file.

**What is left in the queue**: `RunCoordinator` (832, this ticket — the descent phase machine), `GameAdapter` (712), `GameAdapterBridge` (649) and `RemoteIntentApplier`
(621).

## Split 12: the example mod's console half (2026-10-31)

`ModExample.ExampleMod` was 608 lines and its entry named the console examples as the cut, with the note that a reader is meant to see them. They are
`ExampleConsoleCommands` now — the host-relayed echo (the shipped non-local sample), the packet echo and the weight dial, each with its registration and its handler —
and `Bind` calls one method. The mod is **550 lines** and its entry is deleted (four left).

The registration lines themselves were copied, not rewritten: a sample's value is that it shows the framework's contract verbatim (name, description, handler,
parameters, scope, aliases, usage), and the handlers are the same code with the context passed in instead of a field on the mod. The sample still ships as ONE mod:
a reader who opens the example meets the content declarations and the console surface in the same place, one file apart instead of one screen apart.

## Split 13: the container intents (2026-10-31)

`GameAdapter.RemoteIntentApplier` was 621 lines: one switch over the intent kind with a native call per arm. The container family came out first because all four of
its arms share one guard — take an item out of its container, move it into another, expand a container's children into one, drain a liquid stack — and the rule they
share is "run the game's own guard on the owner's real objects, then verify the write on the scene rather than on the call's return".

That is `ContainerIntentApplier` now: `TakeOut`, `MoveInto`, `MoveChildren`, `Drain` and the direct-child snapshot. The applier is **498 lines** and its entry is
deleted (three left). The switch still names every arm, the guards are the same calls in the same order, and the logs still say which item and which container a
refusal was about.

**What is left in the queue**: `RunCoordinator` (832, this ticket — the descent phase machine), `GameAdapter` (712) and `GameAdapterBridge` (649).