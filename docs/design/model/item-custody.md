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
- **A pickup reports the custody change, not only a slot re-home.** A pending-drop child taken onto a ring slot must be charged as a custody change, or a contained kernel record survives its own move: the recorded defect is that "a pending-drop pickup reports only a slot re-home, so a contained kernel record can survive", and the accepted reading confirms the bag's report charges the child and a later slot move is then refused/accepted as the state says.
- **An identity survives a transform.** Whether a worn splint is still the item it was made from was researched and the
  user ruled option 1; all seven acceptance rows pass, so a transform that keeps the item keeps its identity instead of
  minting a new one.
- **An id is never re-issued to a live item.** A restore resumes the allocator above the ids the world already holds: the
  accepted reading is "a new process's continue resumed the host allocator at 241 → 242 and the next allocation took
  counter 242 with no duplicate-id refusal".
- **Registration.** An item's identity reaches the host through the client that allocated it. The accepted record states
  the shape: "`CarriedInventory` (NetMsg 65) is the guest's one-shot registration of the item ids it self-assigned during
  generation. The host uses it to build the per-guest transfer table used by cross-player take/arbitration." Because it was
  one-shot, a registration could be missed; the design is that a client RE-REPORTS its carried inventory when it rejoins
  with a cleared table, and the acceptance reading for that row is "with the host's per-guest table cleared offline and the
  kernel facts left untouched, the rejoin re-…".

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

A held item is in exactly one of these at a time. A container's contents are **not** a body slot: a mechanic that reads
position `slot i` of a body cannot see an item that sits inside the body's backpack.

## Incarnations

Every client shows an item through its own **incarnation** — the object that carries the look, the collider and the
sound. An incarnation is a VIEW:

- it is never the authority for the item's state, and writing to it alone changes nothing;
- the game's own actions are written in terms of the carried item (a body slot), so an incarnation that stands in for
  a carried item answers the slot lookups with "not carried" and every slot-dependent step of a native action misfires
  silently;
- a mechanic that must reproduce a native action therefore runs it on the incarnation that IS the carried item on the
  client that owns it, and never on a stand-in.

## Who judges

The holder's client writes its own side of an item's state and the host arbitrates; an incarnation is written by nobody, because it is a view.

## What travels

Between clients travel the item's identity, its holder, and its state (condition, contents, components, liquid stacks).
Animations, sounds and models never travel as facts: each client derives them from the state it receives.

## What the player sees

An item looks the same in a teammate's hands as in their own: the same model, the same condition-driven appearance, the
same "in use" animation, and the same sounds at the same moments. A difference between the two views is a defect of the
mechanic that produced it, not a rendering detail.

## Gaps

- Which custody change a container's child reports when the container itself moves is recorded per ticket rather than stated here yet.
- The item census recipe answers an instance id; whether every creation path stamps one is not recorded here yet.

## Records

Decisions 155, 156 and 161; `../development/native-parity.md` for the standard; the mechanic pages
`../mechanics/item-use.md` and `../mechanics/give-and-take.md` for the entries that move or consume items.
