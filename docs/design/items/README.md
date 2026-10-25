# Items: the families and what their own actions do

An item is defined by the game's own definition table: its category, its usability flags, its limbs, its liquids, its
components and — the part CUO depends on most — its **use action**, the delegate the game runs when a player uses it. CUO
does not decide what an item does; it decides WHICH entry a gesture is, and then runs the item's own action on the client
that owns the body being affected.

## The families a gesture can be routed to

| Family | The item's own action looks like | Used on another player as |
|---|---|---|
| **Wear** | the item is wearable and carries a desired limb or slot | placed on the target body |
| **Injection** | the item is a container whose action draws or injects liquid | refused on the drag; belongs to the medical operation |
| **Topical** | the item's limb action applies a dose to a limb | applied to the wound view's selected limb |
| **Drink** | the item's use action reaches the container's drink path | the drinker's own client runs the drink |
| **Solid food** | the item's use action eats | the eater's own client runs the eat |
| **Limb tool** | the item has a limb action (not a use action) | applied to the limb the medical view carries |

The classification is by the item's own data, never by its name or category: a container refilled with a different liquid
is classified by what its data now says, and an item that reaches more than one family is separated by the entry the
gesture was made on.

## What the game's own actions do (measured facts the corpus keeps)

These are behaviours the corpus records because a mechanic must reproduce them, not invent around them:

| Definition | Its own use action |
|---|---|
| `pancake` | eats a portion, adds happiness, adds sickness, reduces its condition by one bite, plays the eating sound and a talker reaction |
| `stonefruitclosed` | costs stamina and energy, adds pain, drops the item from the body, consumes itself and picks up an instantiated `stonefruitopen` in its place |
| `waterbottle` (and other drinkable containers) | drinks through the container's own drink path, applying each liquid stack's own effect to the drinker |
| blood bags | draw blood into the bag rather than injecting, which is why an injectable container is not a drag-use item |

An item that consumes itself and replaces itself is the sharpest case in the corpus: the replacement is created by the
game and must be registered like any other created item, and every step of it reads the body's own slots.

## Refusals that belong to items rather than to the gesture

- An item that is not usable at all cannot be used through any entry.
- An item with no authoritative identity cannot be named in a cross-player operation.
- An item that would leave a container behind cannot be handed over; the player is refused rather than given an empty
  container.
- A container with nothing to drink refuses the drink family by name.

## What the player sees

The item's own appearance follows its condition (a bitten fruit looks bitten), the in-use animation and sound are the
game's own, and the item's remaining state after a use is the same for both players.

## Records

`../mechanics/item-use.md` for the entries; `../model/item-custody.md` for identity and custody;
`../../backlog/done/drink-measurement-destructive-use-action.md` for the destructive-action lesson.

## Where mod-authored content belongs

A mod may add content and effects of its own: the platform's registration face exists for exactly that, and a mod-authored effect's consumer count stopped being the trigger ("the user's ruling is that a highly customisable effect surface is the point of the mod platform, so the consumer count is no longer the trigger"). That surface is documented in the mod API reference, not here: this corpus describes the shipped product's behaviour, and a mod's additions follow the same registration rules the reference states.

**What two peers can honestly compare about their content is its ADDRESS.**
`Runtime/Session/Mods/ModContentFingerprint.cs`: the fingerprint is "the ADDRESS every mod registered — the owning mod id, the
entry's canonical id, its kind and its schema version — and nothing else. A definition's other members may COMPUTE their values
(decision 251), so a value is not hashable in general: hashing one would report a difference that is not a difference. The
address is what the registry keys on, what the handshake can carry and what a saved row names." One canonical text has two
readers — "its digest is what the handshake compares per mod and what a cut records in its manifest, so an entry that appears,
vanishes or changes kind or schema version moves both".

## Gaps

None owed: every family page this index names exists, and the shared facts above are the set they must agree on.
