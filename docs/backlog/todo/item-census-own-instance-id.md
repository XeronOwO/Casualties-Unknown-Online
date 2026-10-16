# The body census should name a carried item's own instance id

- Status: Todo
- Priority: Low-Medium
- Category: Acceptance tooling / recipes
- Source: found by the agent-run acceptance of `done/host-rules-replication.md` (batch `20261016-a`,
  2026-10-16): the run needed the id of an item the member really carries to drive `/data set` on it, and
  `item-census mode=body` could not answer it — the reading the run had to take instead is recorded in that
  record's Limits.
- Related: `tools/acceptance/recipes/item-census.cs` (the census and its header),
  `tools/acceptance/recipes/item-provide.cs` (whose `instanceId` is what the run used instead),
  `docs/evidence/acceptance/host-rules-replication-20261016.md` (the run that hit it),
  `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemInstanceId.cs` (the component that carries a live
  object's CUO id), `src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteInventoryItemId.cs` (the marker
  the census reads today)

## What is observed

- `mode=body` builds each entry's `instanceId` from the `RemoteInventoryItemId` component, which only a
  remote DISPLAY PROXY carries. An item the client's own body carries is a real object whose CUO id lives on
  `ItemInstanceId` (attached by the `Item.Start` hook to a runtime-generated item), so the census prints `0`
  for it while the item is right there in the entry.
- The run's own readings: `c5-guest-item-census.json` answered `count: 0` for a fresh body (which is a true
  answer — a new body in this game starts empty), and after `item-provide mode=create type=bandage` put one
  in slot 0 the census's row for it still read no id, while `d12-guest-item-find.json`, seven seconds later, returned
  `instanceId: "5197989525"` for the same item in the same session — `d11`, the create itself, answers `0`, which is the timing this ticket's 2026-10-17 section records. `d16`/`d18`/`d32` (the run's
  local-condition reads) consequently answered `unread`, so the "nothing was applied" half of that ticket's
  row 2 had to be read from the next write's own before-value instead.
- The recipe's header advertises a `returns:` field no mode emits: `ids[]`. `mode=body` emits `items[]`, and
  each entry's `id` is the game's own type id (`bandage`), not a CUO instance id — two different things
  spelled the same way in the same entry.

## Required work

1. Make `mode=body` answer the question a write target asks: the CUO instance id of every item it lists,
   read from the component that actually carries it (the remote marker's id for a display proxy, the local
   component's id for a live object), and say in the entry which of the two the row was read from — a proxy
   and a local item must not become indistinguishable in a field a scenario then acts on.
2. Correct the header's `returns:` line to what each mode emits, so the next run does not plan against
   `ids[]`.
3. Verify live in one client: create an item through `item-provide mode=create`, read the census, and check
   the id it reports for that entry equals the id `item-provide` returned for the same item in the same
   session — the two are read from different components today, which is exactly why they must be compared
   once on a running client.

## What the machine said (2026-10-17, batch `20261017-c`)

**Required work 1 and 2 are DONE, and required work 3 is read on the machine in its two-client form.**
`mode=body` now reads each entry's id from the component that CARRIES it — the local `ItemInstanceId`
(whose id is a property, whose setter registers the object in the runtime index) for a live object, the
remote marker (a field) for a display proxy — every entry carries `idSource`, and the header's `returns:`
line no longer advertises `ids[]`, `localSteamId` or `combineablePairs`, which no mode emits.

Two clients in one session, read 65 s after the world-entry read: the member answers
`id='bandage' origin='slot0' instanceId='9492956821' idSource='local'` for the item it had just created, and
the host answers `id='bandage' origin='proxy' instanceId='9492956821' idSource='proxy'` for that same item —
one id, two carriers, each row naming its own. All three `idSource` values occur in that run.

**What this row is really about is TIMING, and the first form of the run read too early.** Eleven seconds
after the world-entry read the same member row answered `idSource='none'` while the host already showed the
id: `inWorld` is `IsInWorldOrGenerating`, and `ItemIdAllocator.EnsureId` returns `0` while
`HarmonyTraverse.IsGenerating()`. `docs/acceptance/lessons.md` already carries both halves (2026-10-11: wait
for the restore's own completion line, because no id can be allocated while the world is still generating;
2026-10-08: a host's own supplies carry no id until they enter the domain). What stamps a member's item is
that member's own carried-inventory report — `CarriedInventoryReporter` asks
`ItemIdAllocator.EnsureId` for every carried item once the world is no longer generating — so a recipe that
needs an id lets the world settle first, and a HOST's own supply has none at all because no such pass runs
there (the census reports that as `none`, not as a failure).

The paths that call `ItemIdAllocator.EnsureId` (`src/CasualtiesUnknownOnline.GameAdapter/Items/`):
`PickupSync`, `ContainerItemSync`, `ItemSlotSync`, `CraftingSync`, `ItemWorldSync`,
`CarriedInventoryReporter`, `ItemUseSync` and `HeaterCookSync`. Item creation is not among them, and an id is
also attached directly where a row comes IN on the wire or from a save (`ItemStateCodec`,
`WearableRestorer`, `LimbItemTransform`). The evidence is
`docs/evidence/acceptance/item-census-instance-id-20261017-c.md`.

## Non-goals

- Not a runtime change: nothing under `src/` moves; the census is a run-side reader.
- Not a new recipe and not a new mode: same file, same four modes, one component read added and one header
  line corrected.
- Not the local-condition reading the run wanted: a condition read can use `mode=body`'s existing
  `condition` field once the row can be matched by an id at all, which is this ticket.
