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
