# Item identity bridge: a live game `Item` cannot be named by its replicated instance id

- Status: Todo
- Priority: High for the report bounds it unblocks; a prerequisite rather than a bound
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

- `docs/backlog/todo/member-report-bounds.md` — the rows waiting on it, and the ruling that behaviour bounds are out of
  scope (`docs/decisions/active.md`, decision 262).
- `docs/architecture/member-report-validation.md` — the census that names these rows `unbounded`.