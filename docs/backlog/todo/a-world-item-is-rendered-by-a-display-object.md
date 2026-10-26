# A world item is rendered by a display-marked object

- Status: Todo — opened 2026-10-26 from the item-custody projection's own reading.
- Priority: High — the projection exists to prove exactly one claim ("this client shows what the kernel says"), and this is the one
  disagreement a live session produced that is not explained by a departed member.
- Category: Item custody / display objects

## Design

`docs/design/model/item-custody.md` says what the kinds mean: "an incarnation is the row's instance id plus a MARKER", and the
projection reads those markers (`RemoteCloneRender`, `StandingItems.Is`). The rule that decides which kind is expected is
`ItemCustodyComparison.IsDisplayFor`: a WORLD row is REAL on every client — "a world item is simulated locally wherever the scene
shows it, host or guest" — while DISPLAY is only ever another member's CARRIED/CONTAINED/APPLIED row.

So this ticket is the rule's own contradiction: a row saying `World` with an object the adapter has marked as somebody's display.

## The reading

Host log, 2026-10-26 20:18 (`.acceptance` batch session, three clients, the user's own play afterwards):

```
[ItemCustody] rows=287 objects=271 mismatches=6 — the first ones:
  1151372216259:KindDisagrees:World,
  5197989525:RowWithoutObject:Carried,
  5861509230:RowWithoutObject:Carried,
  9492956821:RowWithoutObject:Carried,
  13787924117:RowWithoutObject:Carried
```

The four `RowWithoutObject:Carried` are the departed guest's inventory and are fixed by the roster rule (the mainline ticket's stage-5
note). The first is this ticket: item `1151372216259` is a **World** row, and the object the host has for it carries a display marker.

The same-id trail in the same log, for the reader who wants the history:

```
[ItemPhysics] snap 1151372216259 corrections ended with the follow
[ItemSpawn] ... (the item was originally spawned as a world item)
```

## What has to be established first

1. Which marker the object carries (`RemoteCloneRender` or `StandingItems.Is`) — the projection already knows, the log line does not
   say which; adding the marker to the mismatch line is part of the fix.
2. Whether the object is a **standing materialization** of the member's carried item that outlived its row becoming `World` (the
   materializer's retire path), or a clone render whose row changed kind.
3. Whether the next snapshot repairs it (the reading was steady for at least the last three seconds of that log).

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A row that becomes `World` while a display object stands for it leaves no display object behind on any client — **drop path judged 2026-10-26** (below) |
| 2 | The projection reports `mismatches=0` for a session in which an item goes carried → world (a drop) and world → carried (a pickup) |
| 3 | The mismatch line names the marker, so the next reading locates the object without a second run — **done 2026-10-26**: `LocalObject.Marker` (`clone-render` / `standing`) rides the `Mismatch` and `Describe` prints it, so any reading now carries it |

## Limits

- The reading above came from a session that ended before it could be re-read; the next batch must reproduce it with the marker named.

## The drop path is clean (2026-10-26 20:39, the paradise batch)

`item-provide mode=drop` on the guest dropped a carried `lantern` (slot 3), and the host — the client that held a STANDING materialization of it
(`[StandingItem] materialized lantern (id 9492956821) for …`) — walked the exact chain this ticket exists to check:

```
20:39:26.637 [StandingItem] retired lantern (id 9492956821, owner 76561198863287957): the row left the inventory into the world
20:39:26.641 [ItemDrop] lantern (id 9492956821) not present — requesting materialization at (-1.5,498.1)
20:39:26.647 [ItemSpawn] materializing lantern (id 9492956821) at (-1.5,498.1)
20:39:26.649 [CarriedSync] removed 9492956821 from 76561198887957's snapshot — re-rendering the clone
```

and NO `[ItemCustody]` warning appeared on any client afterwards. So the carried→world transition retires its display object and materializes the world
copy, which is acceptance row 1 for this route; the route that produced `1151372216259:KindDisagrees:World` is still unknown, and the next reading
will name its marker (`clone-render` / `standing`).