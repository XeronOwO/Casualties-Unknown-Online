# Item identity bridge: a live game `Item` cannot be named by its replicated instance id

- Status: Resolved — closed 2026-10-24 (no code; the resolution below is re-verified against the current
  tree).
- Priority: Low — resolved without code
**Found:** 2026-10-18, while trying to give the building-open report a data anchor.

## The gap

A report that says "I opened/attacked that" can only be bounded by DATA if the host can compare the tool the reporter
used with the items it holds — and that needs the reporter's client to name the tool. It cannot:

- The game's own `Item` carries no instance id. `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs` has no such member
  (searched for `instanceId`, `InstanceId`, `instanceID`).
- CUO has no accessor that turns a live game `Item` into the replicated `ulong` instance id. The direction that exists is
  the opposite one: `ILocalItemAccess.ReadLocalItem(ulong instanceId)` and `WriteLocalItem(ulong instanceId, …)` address
  an item BY instance id, and the messages carry `ItemInstanceId` (`PlayerDomainModule` reads `c.ItemInstanceId` off a
  message before building a command).
- No map is kept anywhere: there is no `Dictionary<Item, ulong>` or equivalent in `src/`.

So an adapter patch holding a live `Item` — for example `Minigame.game.currentItem`, the lockpick the open minigame
uses (`reversing/.../LockpingMinigame.cs`: `NeedsItem() => this.pickLevel >= 0`, `Minigame.game.currentItem`) — has no
way to say WHICH item it means in CUO's vocabulary.

## What has to be true when this lands

- One accessor that answers "the replicated instance id of this live game item" (and the inverse, if the write path
  needs it), owned by whoever already adopts/restores items, so the mapping is maintained where items are created and
  destroyed rather than guessed at each call site.
- A stated answer for the lifecycle questions: what it returns for an item that is not replicated yet, for a destroyed
  object, and for an item adopted during a restore.
- Its first consumer is the open report's tool identity; the building-damage row is the second, and it must not be built
  "just in case" before one of them needs it (the mechanism-consumer rule).

## Why this is a ticket rather than a line

The mapping has to be maintained across the whole item lifecycle (spawn, adopt, transfer, container move, destroy,
restore). That is a subsystem property, not a lookup; treating it as a one-line accessor is how it would end up wrong
for exactly the cases that matter (a tool inside a container, an item restored from a save).

## Related

- `docs/backlog/done/member-report-bounds.md` — the rows waiting on it, and the ruling that behaviour bounds are out of
  scope (`docs/decisions/active.md`, decision 265).
- `docs/architecture/member-report-validation.md` — the census that names these rows `unbounded`.
## Resolution 2026-10-18: no subsystem is needed, and its only consumer turned out to have no anchor

The premise above ("the mapping has to be maintained across the whole item lifecycle") was checked by the independent
review of an attempt that used it, and it is wrong:

- The index already exists in BOTH directions once one member is added. `ItemInstanceId` holds `ById` (id → component)
  and `Register()`/`Unregister()` run on the live object, so a reverse `TryFindId(Item, out ulong)` is a dictionary
  entry, not a lifecycle subsystem. The review confirmed the registration order is safe: every path that creates a
  replicated item (`ItemStateCodec`, `StandingItemRecipe`, `ItemIdAllocator.Allocate`, `CarriedInventoryReporter`'s
  `Ids.EnsureId`) has the `Item` component on the object BEFORE the id is assigned, so "item created before it has an
  id" does not leave a hole.
- Its only consumer was the open report's tool claim, and that claim has just been concluded unanchorable for a reason
  that has nothing to do with identity: empty-handed opening is legal gameplay, so a zero id must be accepted and the
  claim cannot be falsified (`docs/backlog/done/member-report-bounds.md`, third attempt).

So there is no code to develop here. If a future consumer needs "name this live item", the one-line reverse lookup is
the whole change; nothing has to be built or maintained ahead of it. Recorded rather than kept as a pending ticket so
nobody builds a subsystem for a consumer that does not exist.

## Re-verified (2026-10-24)

- `CasualtiesUnknownOnline.GameAdapter.Items.ItemInstanceId` still holds the id → component index (`ById`) and
  registers/unregisters itself when `Id` changes, so the stated reverse lookup is one dictionary entry over
  the map that already exists; `TryFindItem(ulong, out Item)` is the forward accessor the claim names.
- The premise's consumer check still holds: the open report's tool claim remains unanchorable
  (`done/member-report-bounds.md`), and no consumer of a reverse lookup exists in `src/`. Nothing was built,
  as the resolution asks.
- Closed as `Resolved` (the folder's meaning: a decision resolved without further code action); the record
  stays the answer for the next reader who asks why no identity subsystem exists.