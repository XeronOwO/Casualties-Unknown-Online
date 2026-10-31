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