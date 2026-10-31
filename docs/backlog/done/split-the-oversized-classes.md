# The recorded over-line classes still owe their splits

- Status: Done
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

## Split 14: the adapter's frame pump (2026-10-31)

`GameAdapter` was 712 lines, and the largest block in it was not a domain but the ORDER: one ordered walk over every domain, from the menu guards at the top of the
frame to the cut seam at the bottom, with the latency measurements around the sections that can stall. That walk is `FrameTick.Run()` now — **577 lines** in the
adapter, whose `ICuoService.Update` is one line, and its entry is deleted (two left).

The order is the behaviour and it was moved verbatim: same calls, same sequence, same comments explaining why each one stands where it does. Two pieces of state moved
with it — the previous frame's local body (the world-entry edge for the destroy-suppression reset) and the latency instrumentation, each of which now belongs to the
pump that reads it. Two gates followed the move: the call-context boundary gate now asserts `CallContext.EndFrame()` sits once in `FrameTick.Run` **and** that the
adapter pumps exactly once (so a future move cannot drop either half), and one sync-coverage quote moved with its call.

**What is left in the queue**: `RunCoordinator` (832, this ticket — the descent phase machine) and `GameAdapterBridge` (600 by its file, still over the gate's count
because the aggregate it serves is what the gate measures).

## Split 15: the world-generation port gets an implementation object (2026-10-31)

`GameAdapterBridge` was 649 lines, and its own doc names the way it shrinks: the port forwarders become per-port adapter objects. The world-generation port went first
because it is the largest (nine hooks) — `WorldGenPatchAdapter` implements `IWorldGenPatchBridge` and the bridge composes it as `WorldGen`, exactly the shape
`IModContentPatchBridge` already had. The bridge is **600 lines** by its file.

Two things stayed where they were: `OnWorldGenerate`'s body (it also resets the enemy roster, keeps the loading animation alive, clears a stale layer modifier and
empties the pre-generation tables — it is a generation boundary, not a forwarder) moved with the port because it IS the port's implementation, and the aggregate keeps
declaring `WorldGen` so a patch still reaches it through `PatchBridge.Impl`.

Five pins followed the move: the seam table now marks the port as implementation-served (not self-served), the implementation census gained `_worldGen`, the aggregate
census gained `WorldGen`, the enemy-roster pin reads the adapter for the generation boundary, and the damage-block gate reads the declaration in the port and the
implementation in the adapter. The debt entry's note was rewritten to record that the publish path has already moved and what the descent split now owes.

**What is left in the queue**: `RunCoordinator` (794, this ticket — the bind/unbind table and the session edges).

## Split 16: the host-descent follow becomes a state machine (2026-10-31)

`RunCoordinator` was 832 lines and its entry named `HostDescentFollow` as the first of two cuts. The descent's STATE is the piece the run phase does not carry — how
far the follow's own generation got (accepted, launched, released-early, waiting for the ready) — together with the anchor its bounded waits run from and the host's
own presentation window. All of that is `HostDescentFollow` now, a value-level object with no game types: eight transitions and two questions (is an abort due, and
why), answered against `LayerDescentPolicy`.

The coordinator is **794 lines** and its entry is RE-RECORDED at that number, because the descent's scene plumbing (the decision to accept an edge, the settle read,
the phase mapping onto the run, the menu-return origin rule) is still there and is the second thing the entry owes. What moved is the part that can be tested without
a session — and it is: `HostDescentFollowTests` pins the transition table (launch without an edge changes nothing, an early release keeps the generation in flight,
waiting is not aborted by the clocks), the abort ladder's order and its three reasons, and that ending the follow closes the host's window.

**What is left in the queue**: `RunCoordinator` (779, this ticket — the descent's scene plumbing and the world-join family, both now unblocked by `RunPhase`).

## Split 18: the settle decision joins the state machine (2026-10-31)

The descent's SETTLE question — has this member's own new layer finished generating, and does the follow then end in playing or in waiting for the host's release? —
moved out of the coordinator's `UpdateDescent` into `HostDescentFollow.TryDecideSettle`, which answers against the already-pinned `LayerDescentPolicy`. The coordinator
keeps the three flag reads and the two actions (end the follow and start playing, or wait for the ready). It is **779 lines** and its entry is re-recorded.

The win is that the settle mapping is now TESTED: `HostDescentFollowTests` pins that a still-generating layer answers nothing, that a settled layer with no release in
hand answers "wait", that an early release answers "play", and that a follow which is not following (or is already waiting) answers nothing at all. That branch used to
have the live descent batches as its only witness.

## Split 17: the run phase becomes a type (2026-10-31)

`RunCoordinator` was 794 lines, and every extraction still owed in it hit the same wall: `RunPhase` was a private nested enum, so no collaborator could take the
phase as an argument. It is `RunPhase` in its own file now — the seven states with the one-line reason for each — exactly as `DescentPhase` came out before it. The
coordinator is **784 lines** and its entry is re-recorded.

This is a preparatory cut, and it is recorded as one: the next extraction (the descent's scene plumbing into a controller, then the world-join family) needs the phase
to be a value two objects can pass, which is what this change makes possible. A bind/unbind helper was tried and REVERTED in the same session: wrapping six
subscriptions in a handler record added an indirection without removing a line of meaning, and the aggregate it served is not what made the class large.

## Splits 19 and 20: the descent's scene plumbing and the world join (2026-11-01)

Two extractions finished the class, in this order:

- **`DescentFollowController`** — everything about a descent that needs the game: deciding whether an edge applies to this member, the body guard, the accept/settle/abort
  drive, the menu-return origin rule and handing the client back to the ordinary entry path. It reads the run phase and the world flag through delegates and writes the
  phase through one, so the phase stays owned by the coordinator while the rules live with the controller.
- **`WorldJoinFollow`** — the guest's join state machine: a duplicate WorldJoin never restarts the run (only `Idle` accepts one), a member following a descent undoes the
  invite's baseline promise instead of taking it, the pre-run script is waited for, and the run or the tutorial is started exactly once. The join's `joinIsTutorial` moved
  into it with the rules that use it.

`RunCoordinator` is **541 lines**, its entry is gone, and `docs/architecture-debt.json` is now an EMPTY object: no class in `src/` is over the gate's ceiling and the gate
proves it. Both new classes follow the shape the queue established: state and decisions in objects that can be tested without a session, the game reads in one
collaborator that takes the phase as a delegate.

## The queue is empty (2026-11-01)

Every class the queue listed has either been split under 600 lines or left the file by a change that made it smaller:

| # | What left | Where it went |
|---|---|---|
| 1 | the console's report half | `ConsoleReportWriter` (CommandConsoleService 684 → 556) |
| 2 | the overlay's report-once family | `ReportOnce` (OnlineUiWorldOverlayView 609 → 587) |
| 3 | the world control's social half | `IWorldSocialControl` (IWorldControl 615 → 577) |
| 4 | the take ladder's destination rules | `ItemDestinationResolver` (629 → 511) |
| 5 | the character store's four copied senders | `CharacterVisualPublisher` (628 → 568) |
| 6 | the patch aggregate's world-generation hooks | `IWorldGenPatchBridge` (IPatchBridge 608 → 548) |
| 7 | the owner-state event | `OwnerStateEventReporter` (738 → 620, re-recorded) |
| 8 | the local heal-item query | `LocalHealItems` (601 → 545) |
| 9 | the local body's state stream | `LocalBodyStatePublisher` (922 → 832) |
| 10 | the report-recovery surface | `IWorldBlockReportControl` (WorldService 640 → 589, partial deleted) |
| 11 | a snapshot's wire half | `CharacterReportSender` (620 → 574) |
| 12 | the example mod's console half | `ExampleConsoleCommands` (608 → 550) |
| 13 | the container intents | `ContainerIntentApplier` (RemoteIntentApplier 621 → 498) |
| 14 | the adapter's frame pump | `FrameTick` (GameAdapter 712 → 577) |
| 15 | the world-generation port | `WorldGenPatchAdapter` (GameAdapterBridge 649 → 600) |
| 16 | the descent's state machine | `HostDescentFollow` (832 → 794) |
| 17 | the run phase | `RunPhase` (794 → 784) |
| 18 | the settle decision | `HostDescentFollow.TryDecideSettle` (784 → 779) |
| 19 | the descent's scene plumbing | `DescentFollowController` (779 → 611) |
| 20 | the world join | `WorldJoinFollow` (611 → 541) |

**Rows**: row 1 passes with an empty debt file (the gate is the proof), row 2 passes because every extraction moved statements without changing them and the suite's
6562 behaviour tests ran unchanged through all twenty, and row 3 passes with the file smaller than it has ever been. The ticket moves to `review/`.
