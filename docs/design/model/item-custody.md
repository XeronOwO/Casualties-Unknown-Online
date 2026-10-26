# Item custody: identity, ownership, and what an incarnation may decide

An item in CUO is one authoritative identity that several clients show at once. This page states the rules that hold
for every item mechanic; the mechanic pages cite it.

## Identity

- Each item has one **instance identity**, allocated once and stable for its whole life: it survives being dropped,
  picked up, moved into a container, restored from a save and handed to another player.
- An item that has no identity cannot be named in a cross-player operation; the operation refuses it by name instead of
  acting on "the first match".
- A catalogue definition (what an item IS) is shared; an item's state (its condition, its liquids, its components, its
  contents) belongs to that instance.
- **Creation is registered BEFORE the item is operated on.** The user's 2026-09-18 ruling names what it replaced: "the
  500 ms pickup hold is a design smell, not a latency fix", and the invariant it leaves is creation-before-operation — a
  creation is judged first, and a refused creation's later operation is answered rather than applied.
- **The ordering is ordering, never a waiting window.** `src/CasualtiesUnknownOnline.Runtime/Session/Items/PendingItemCreations.cs`
  states the boundary: "an operation report naming an item may only leave after every creation report that item depends on has
  left, so the host never has to wait on, or guess about, an operation on an item whose creation it has not judged yet", and
  then rules the temptation out — "this is ORDERING, never a latency window: nothing here waits, expires or measures time. A
  settle either reports the creation right now or leaves it to the frame-end flush that was already due".
- **A pickup reports the custody change, not only a slot re-home.** A pending-drop child taken onto a ring slot must be charged as a custody change, or a contained kernel record survives its own move: the recorded defect is that "a pending-drop pickup reports only a slot re-home, so a contained kernel record can survive", and the accepted reading confirms the bag's report charges the child and a later slot move is then refused/accepted as the state says.
- **An identity survives a transform.** Whether a worn splint is still the item it was made from was researched and the
  user ruled option 1; all seven acceptance rows pass, so a transform that keeps the item keeps its identity instead of
  minting a new one.
- **An id is never re-issued to a live item.** A restore resumes the allocator above the ids the world already holds: the
  accepted reading is "a new process's continue resumed the host allocator at 241 → 242 and the next allocation took
  counter 242 with no duplicate-id refusal".
- **An id a client allocates itself is namespaced by that client.** The high half is the client's own counter and the low
  half names who allocated it, which is why two clients never mint the same id and why the host's table can be assembled from
  reports rather than assigned centrally. The precise form is worth stating because one summary calls it loosely: the message
  says "ids are (counter << 32) | SteamId, so the guest can allocate without host round-trips", while
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemIdCoordinator.cs` — the code that owns the space — is exact: "ids
  are (counter << 32) | account-low … the space is per ACCOUNT (`ItemInstanceIds`: the owner half is the account id's low 32
  bits, so it never equals the full SteamId)", and so a per-space watermark is all the coordination needed.
- **A rejoined client's counter is resumed, not restarted.** The coordinator keeps "per-guest counter watermarks (guest to
  host reports, host to guest grants on handshake completion)" because "a crashed-and-rejoined guest's counter restarts from
  zero and would reuse ids the host's tables still hold". The registration itself travels as "an ABSOLUTE set and repeated on
  the cadence of `CarriedInventoryReportSchedule`" — sync-coverage row I8: "one frame lost in the lazy-P2P swallow window must
  heal without a reconnect, and ids the guest self-assigns later (a crafted product) must converge too".
- **Registration.** An item's identity reaches the host through the client that allocated it. The accepted record states
  the shape: "`CarriedInventory` (NetMsg 65) is the guest's one-shot registration of the item ids it self-assigned during
  generation. The host uses it to build the per-guest transfer table used by cross-player take/arbitration." Because it was
  one-shot, a registration could be missed; the design is that a client RE-REPORTS its carried inventory when it rejoins
  with a cleared table, and the acceptance reading for that row is "with the host's per-guest table cleared offline and the
  kernel facts left untouched, the rejoin re-…".
- **A refused creation is remembered as a tombstone, bounded and per session.**
  `src/CasualtiesUnknownOnline.Application/Kernel/RefusedItemCreations.cs` keeps the ids the host judged dead (a block break
  that lost first-writer-wins, a spawn command the kernel rejected) because "the id is never registered, so without this a
  later operation on it would look exactly like an operation on an item whose creation was never reported at all — and that
  one is a protocol violation". The set is "bounded (oldest evicted first) so a hostile or buggy peer cannot grow it without
  limit, and cleared when the session ends: item ids are session-partitioned".

## A client's own result is reconciled

A pickup or a drop a client performed is reconciled against the host's table rather than trusted once: the accepted record
closed the case where a guest's "local pickup/drop result is not reconciled", and the family it belongs to is the one that
heals a swallowed report by a bounded re-report. The rule is that an action a client believes it completed is confirmed
against the holder's table, so a lost message does not leave the two views apart.

## A player's own marks on an item

A favourite mark is made by a player over an item they hold, and it is not a remote operation: decision 258 states that the
mark "belongs to the player who made it, is saved with that player's character record, and never reaches the owner's item" —
the owner's item is the OTHER player's. The accepted ticket's reading is that the mark "became the item's own flag once the
item changed hands, and the same key over the player's OWN inventory kept the game's own behaviour", and the kernel records
it as the item's own flag (`src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemData.cs` declares `bool Favourited`).
So a mark travels with the item it was set on, never as a claim about another player's item.

## Custody

| Custody | Meaning | Authority |
|---|---|---|
| **Body slot** | the item is in one of a body's own slots | the body's owner client writes that body's item state; the host arbitrates |
| **Container** | the item is inside another item that holds contents (a backpack, a pouch, a bag) | same rule, with the container as the holder |
| **World** | the item lies in the world, or is held by a world entity | the host writes; every client projects it |
| **In flight** | the moment between two holders during a transfer | the host owns the decision; the clients show the animation |

The wire enumerates the families and names two of them explicitly: `src/CasualtiesUnknownOnline.Protocol/Wire/WireItemLocationKind.cs`
declares `World = 1`, `Carried = 2`, `Contained = 3`, `Terminal = 4` and `Applied = 5`, and
`WireItemLocation.cs` carries "the kernel's single authoritative item location" as a kind plus its owner, parent item id and
position. The two the table above folds into other rows are **Terminal** (an item a body's terminal state holds) and
**Applied** (an item applied to a limb — the splint, the limb tool, the bandage). A Terminal item's reason is itself a typed
fact: `src/CasualtiesUnknownOnline.GameState/Domains/Items/TerminalKind.cs` declares `Consumed`, `Destroyed` and
`ReplacedBy`, so "this item is gone" always says why.

**Applied is not an inventory position, and worn is not Applied.** `Domains/Items/ItemLocationKind.cs` states both rules:
Applied "is the one family that is NOT an inventory position — the item is the installed form of a limb component … it is
held by the limb rather than carried, and no inventory gesture reaches it", and it is "deliberately NOT the wear encoding — a
worn item is CARRIED with a negative slot index, so it is still in the inventory and a search can reach it".
`Domains/Items/LimbItemLocationRules.cs` adds that both directions (onto the limb and off it) are relocations that keep the
instance id, "so a mod that recorded the id … can follow it across the transform", and that the rules do not decide which limb
may carry which component: the game's own data gates that on the side that runs the action, a refused apply spends nothing,
and no report is produced for it.

A held item is in exactly one of these at a time. A container's contents are **not** a body slot: a mechanic that reads
position `slot i` of a body cannot see an item that sits inside the body's backpack.

**A location is one shape for every family, so a caller reads the kind first.** `Abstractions/IModItemState.cs` states the
rule the kernel's own model follows: "a member a location does not use carries its neutral value, the same one-shape rule the
framework's own location model follows, so `Location` is what tells a caller which members mean something: the owner and the
limb for `Applied`, the owner for `Carried` and …". A caller that reads a field before the kind is reading a neutral value it
cannot tell from a real one.

**A container's contents are discrete authoritative state, not a stream.**
`src/CasualtiesUnknownOnline.GameState/Domains/Items/SyncContainerItemsCommand.cs` reconciles "one container subtree in a
single atomic batch: the parent fact update plus create/update/move/destroy of every contained child", and its own summary
gives the reason: "container contents are authoritative discrete state, so this is a reliable kernel command, never a state
stream". A client's own item state is likewise typed rather than free-form —
`ItemComponentFieldKind.cs` allows `Float`, `Int`, `Bool`, `String`, `StringList` and `Enum` fields, and nothing untyped.

**A cancelled departure owes an ARRIVAL report, or the child dies by omission.**
`src/CasualtiesUnknownOnline.Runtime/Session/Items/ContainerArrivalPolicy.cs` states the trap exactly: "the kernel records a
container's children as the CONTAINER's rows … while the report a cancelled departure sends on the normal path is the slot
move's, which is DATA ONLY (`ItemUpdateState`). So an item released out of a container keeps its old contained row until the
next container report for that parent arrives — and that report does not name it, so `ItemDomainModule.DecideSyncContainer`
takes it to `Terminal (ReplacedBy)`, which no later command may revive". A cancelled departure therefore sends the arrival
report that puts the child back, not just its own state update.

**A display proxy's children never enter the report chain, and a non-authoritative copy never presents its own impact.**
`GameAdapter/Patches/RemoteCloneContainerGuard.cs`: a container inside a render clone is "presentation-only and must never
enter the item-domain report chain". `NonAuthoritativeItemImpactGuard.cs` applies the same rule to collision presentation on a
guest's world-item copies, so "the session/authority decision stays in one place instead of being duplicated per script".

## Incarnations

Every client shows an item through its own **incarnation** — the object that carries the look, the collider and the
sound. An incarnation is a VIEW:

- it is never the authority for the item's state, and writing to it alone changes nothing;
- the game's own actions are written in terms of the carried item (a body slot), so an incarnation that stands in for
  a carried item answers the slot lookups with "not carried" and every slot-dependent step of a native action misfires
  silently;
- a mechanic that must reproduce a native action therefore runs it on the incarnation that IS the carried item on the
  client that owns it, and never on a stand-in.

**An incarnation's identity is the row's instance id plus a marker, and that is the whole space it needs.** The adapter
already stamps both halves on every object it creates for a row: the item carries its `ItemInstanceId`, and the two objects
that stand in for somebody else's row carry their own marker — `RemoteCloneRender` for a clone-render slot/limb child and
`StandingItemObject` (read through `StandingItems.Is`) for a materialized standing object. So a display incarnation is
recognised by "a marker is present", its row is the id it carries, and no second identity space is invented: a display that
had one would be a second place to keep in step, which is the defect the rest of this page exists to end. The custody
projection (`GameAdapter/Items/ItemCustodyComparison.cs`) reads exactly those two markers, which is why the comparison and the
renderer can never disagree about what an object is.

**The projection only asks about the members who are HERE.** A row's owner can leave while the row stays: the kernel keeps a
departed member's carried rows — that is where their inventory lives — and the display objects for them are destroyed when they
leave. So a `RowWithoutObject` for an owner who is not in the roster is not a disagreement, and the comparison takes the roster
(`Compare(..., presentOwners)`, `IsOwnedBySomeonePresent`) and skips those rows. The first reading that needed it was the host's
own log on 2026-10-26 20:18, which reported three of a departed guest's carried ids every second
(`5197989525:RowWithoutObject:Carried`, `9492956821:RowWithoutObject:Carried`, `13787924117:RowWithoutObject:Carried`); a
`KindDisagrees` on a `World` row is a different defect and is not excused by this rule.

## Who judges

The holder's client writes its own side of an item's state and the host arbitrates; an incarnation is written by nobody, because it is a view.

**A remote inventory operation is judged by three roles, and each role owns one thing** (decisions 218-221):

- the **requester's client** decides which native gesture happened and what the intent is, from its own view; it never mutates a display proxy
  and never predicts the owner's item state;
- the **host** decides only what is genuinely its own: the session permission, membership, the ownership fact and first-writer-wins arbitration
  between competing intents. It never models inventory contents, never edits a clone of the owner's character data, and never judges reach or a
  body from stale positions;
- the **owner's client** performs the operation — the native call on the real objects, with the real guards, animations and sounds — and reports
  the authoritative result.

**The family's one permission is the host rule `RemoteBackpackAccess`** (decision 263): `allow` (any visible member), `unconscious_or_dead` (the
operation only on an unconscious or dead other player), `deny` (nothing). The middle value is not "hide the pack": a conscious owner's backpack
opens **view only** — items visible, red body slots, every operation refused with one native alert line that says why — while the double-Tab take
closes the view before it releases, so the read-only state is captured at the proxy's pickup and consulted at the release. The host is the verdict
at two seams (the intent service and the transfer service), always judging the OTHER player; the middle value fails closed when that player's health
is unknown. The policy can only close a surface, never open one the landed take rule keeps shut.

## What travels

Between clients travel the item's identity, its holder, and its state (condition, contents, components, liquid stacks).
Animations, sounds and models never travel as facts: each client derives them from the state it receives.

**An intent travels as the native call it replays, never as a CUO taxonomy and never as a mirrored result**: the call's identity plus its
operands, keyed by authoritative instance ids. The vocabulary is the native call set — move into a container and take out of one, pick up to a
slot, swap slots, drop, drop a wearable, use, wear, combine, load and unload a battery, drain, apply to a limb, hand to a trader, the
container-children loop, and the two-sided cross-player transfer. Two members are one intent each only because the native world has no
cross-player call: `TransferToBody` and `GiveToBody` are the native release on one side and the native pickup on the other, with the host deciding
the item id first-writer-wins between them.

**The classification is the game's own.** CUO intercepts the native gesture's MUTATION CALLS inside a call bracket that spans exactly one native
invocation, not the dispatch and not a table of its own: the game's ordering, its predicates and its early exits all execute, and only the mutation
is recorded and skipped so its internals never run on a proxy. A gesture CUO has never seen is logged as unclassified rather than silently doing
nothing — the fail-closed rule. The bracket answers the body-shaped predicates from the body the ring shows, because the native branch reads the
LOCAL body while the ring and the dragged item belong to the displayed clone (decision 218).

**A favourite mark is the local player's own and is saved with that player's character record** (decision 258): the keystroke in somebody else's
backpack never changes an item that player owns, so there is no intent for it, and the mark cannot be applied in another world.

## What the player sees

An item looks the same in a teammate's hands as in their own: the same model, the same condition-driven appearance, the
same "in use" animation, and the same sounds at the same moments. A difference between the two views is a defect of the
mechanic that produced it, not a rendering detail.

Feedback splits by where it is produced, and needs no second channel: the branch-level feedback the game's own branch makes runs on the
requester's client (the backpack sound, alerts, slot scale, radial and container-window state), while a sound or animation produced INSIDE a
mutation call happens where the mutation happens — on the owner's client — and reaches the others through the existing sound and state paths.

## Gaps

- Which custody change a container's child reports when the container itself moves is recorded per ticket rather than stated here yet.
- The item census recipe answers an instance id; whether every creation path stamps one is not recorded here yet.
- **The adapter does not read the location this page makes authoritative.** `ItemLocation` appears zero times under
  `src/CasualtiesUnknownOnline.GameAdapter/`, while five adapter-side stores each keep their own copy of "where is it" and three layers infer
  intent from Unity lifecycle callbacks (`ItemWorldSync`, `PickupSync`, `ItemReconcile`). Both item losses of 2026-10-25 came from that gap, not
  from a missing rule; the work is `../backlog/todo/item-custody-single-authority.md`. **Progress (2026-10-25):** the projection exists and is
  judged on two clients — the rows and this client's objects agree at 285/285 on both ends, `[ItemCustody] mismatches=0` — so what remains is
  switching the readers to it and deleting the copies, not closing this gap.

**The limits the remote-inventory design recorded, carried here so the merge into this corpus loses nothing** (the page itself is retired by
`../backlog/todo/merge-remote-inventory-design-into-corpus.md`): the two-client feel — native animation, sound and timing on the operator's
screen — is the release-cycle acceptance and no test can produce it; an item sound produced INSIDE a replayed call is heard on the owner's client
alone, because a remote-driven replay runs under the remote-apply scope whose sounds are deliberately not attributed to the local player
(`../backlog/review/suppressed-native-call-sounds-stay-unheard.md`); the radial weight readout prints the operator's own encumbrance, because the
native code reads the local body and the clone carries no encumbrance projection; an owner's body runs no release animation for the cross-player
custody move, ruled accepted (decision 240) because the native world has no such call to replay; and no automated three-client test drives the
third peer's view.

## Records

Decisions 155, 156 and 161 for custody itself; 218, 219, 220 and 221 for the remote-intent stages this page now states as rules; 258 for the
local favourite mark; 240 for the accepted cross-player release-animation limit; 263 for `RemoteBackpackAccess`. `../development/native-parity.md`
for the standard; the mechanic pages `../mechanics/item-use.md` and `../mechanics/give-and-take.md` for the entries that move or consume items.
