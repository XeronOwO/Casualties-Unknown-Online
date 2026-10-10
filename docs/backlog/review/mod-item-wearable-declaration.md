# The mod item's wearable declaration: the placement the game's own wear flow reads

- Status: Review — **cut and landed 2026-10-11** out of `docs/backlog/todo/mod-content-ceiling.md` Part 3.A,
  which lists the wearable set as one entry and whose promotion rule puts a defect of a surface the API
  already promises ahead of the capability entries. This entry is one: `IModItemDefinition.Wearable` is
  declared today, and the game's own wear flow THROWS on it (below). The code half is complete and the
  independent FULL-tier review raised no blocker; its majors and minors were fixed in the same commit (see
  *What landed* and *Review round*), so what remains is the acceptance batch's own row and nothing to
  develop. The cycle's family audit also corrects a claim the sibling ticket carries:
  `docs/backlog/review/mod-declared-behaviour-with-no-function.md`'s non-goals say the remaining Part 3.A
  gaps are "none of them is a crash" — the wearable half is one.
- Priority: High
- Category: Mod platform / content binding
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 3.A)
- Related: `docs/backlog/review/mod-declared-behaviour-with-no-function.md` (the sibling ruling this ticket
  follows: a declaration the framework cannot materialize is inert and reported, never fatal),
  `docs/backlog/review/mod-cross-player-native-semantics.md` (the wear chain whose predicate already asks
  the item's own data and refuses what it cannot place), `docs/backlog/review/mod-content-typed-registration.md`
  (the typed content contract this one extends), `docs/backlog/todo/mod-authored-effects.md` (the
  code-registration surface — the wear fields are DATA, not functions, so this ticket does not wait for it),
  `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md`,
  `docs/decisions/active.md` (184: the affected side judges on its own picture)
- Source: the 2026-09-25 mod-ceiling inventory (Part 3.A's wearable set), promoted 2026-10-07 by the user's
  directive that content customisation is the point of the platform; cut 2026-10-11.

## Why this ticket exists

`IModItemDefinition.Wearable` is a `bool`, and the adapter writes it straight into the vanilla flag:
`ModItemInfoFactory.Build` sets `wearable = definition.Wearable` and assigns neither
`ItemInfo.desiredWearLimb` nor `ItemInfo.wearSlotId`. The game's own wear flow needs both to place the
item, and reads them with no null check — `Body.WearWearable` (<c>Body.cs:1480-1498</c>):

```csharp
Item wearableBySlotID = this.GetWearableBySlotID(item.Stats.wearSlotId);   // :1482
...
Limb limb = this.LimbByName(item.Stats.desiredWearLimb);                   // :1493
if (limb.dismembered)                                                      // :1494
```

`LimbByName` returns null for a name no limb carries (<c>Body.cs:1476</c>), so line 1494 dereferences
null: **a mod item that declares `Wearable` and is then worn throws a `NullReferenceException` inside the
game's own wear flow.** Nothing else has to be wrong — the declaration alone is enough, because the flag
is the only thing the API can express.

**Four** callers reach that dereference, and a `wearable` flag selects all of them — the census of
`LimbByName` in the game assembly (`Body.cs:1493`, `:1541`, `:1558`, `:1575`) is the whole family:

- the radial menu's centre drop — `PlayerCamera.TryPerformRadialAction` (<c>PlayerCamera.cs:1640-1643</c>)
  runs `this.body.WearWearable(this.dragItem)` for any `wearable` item; the `DoPickupCheck` gate returns
  true immediately for a held item (<c>Body.cs:1358</c>), so nothing else stands in the way;
- the touch auto-wear — `Body.AutoPickUpItem` (<c>Body.cs:500-528</c>) routes every `wearable` item to
  `WearWearable` instead of a slot;
- a save load — `SaveSystem.cs:338` puts the saved worn items back through the same call;
- and CUO's own replay, `RemoteIntentApplier.ApplyWearItem` (which already refuses a non-wearable item).

Three more members dereference the same name with no null check, so the flag reaches a crash from a CARRY
and a pickup check as well as from a wear: `Body.GetWearable(string)` (<c>Body.cs:1541</c>, called by the
drag release at <c>PlayerCamera.cs:1698</c> and by the drop report), `Body.HasWearable(string)`
(<c>:1558</c>) and `Body.HasWearable(Item)` (<c>:1575</c>, asked by `Body.DoPickupCheck` about a carried
container and by two tutorial courses).

CUO's own cross-player chain does NOT crash, and that is the second half of the problem: `WearAdmission`
asks `IWearSemantics`, `GameWearPlacement.TryResolve` refuses an item whose `desiredWearLimb` is empty, so
a mod garment is simply not carried between players. The local path is the one that breaks, and the mod
author has no member to declare the placement with — the same "declaration versus materialization" shape
this project has now closed four times (`mod-content-kind-with-no-provider`, `mod-crafting-quality-labels`,
`mod-declared-behaviour-with-no-function`, and this one), in the version where the game's own path dies.

## What the game's own wear flow reads (measured 2026-10-11, `reversing/Assembly-CSharp/Assembly-CSharp/`)

| `ItemInfo` field | Native reader | What it decides |
|---|---|---|
| `desiredWearLimb` | `Body.WearWearable` → `LimbByName` (<c>Body.cs:1493</c>) | the limb the garment parents to; a name no limb carries is the NRE above |
| `wearSlotId` | `Body.GetWearableBySlotID` (<c>Body.cs:1590</c>), called at <c>:1482</c> and <c>:522</c>; the save file records it as the worn item's slot | the occupancy comparison — two wearables in one slot collide even on different limbs |
| `wearableCanBeHeld` | <c>PlayerCamera.cs:1609</c> | the inventory drag refuses to move such a garment (the game's own `alertpickupwearable`) |
| `wearableArmor` | <c>Limb.cs:303</c>/<c>:313</c>, tooltip <c>PlayerCamera.cs:1210-1218</c> | how much of a hit on the covered limb is absorbed |
| `wearableIsolation` | <c>Body.cs:3552</c> (`clothingTemperature`) | cold protection |
| `wearableHitDurabilityLossMultiplier` | <c>Limb.cs:315</c>, <c>GroundGlass.cs:16</c> | how fast the garment loses condition when hit |
| `wearableVisualOffset` | `Body.WearWearable` (<c>Body.cs:1507</c>) | the worn sprite's sorting order (vanilla default 5) |

The limb vocabulary is the one the API already uses elsewhere: `IModItemLimbWornSprite.LimbName` and
`IModLimbMoodleBinding.LimbName` are both documented as "the same name used by the game's `Body.LimbByName`
lookup". This ticket reuses it rather than inventing a CUO enum, and the name is compared exactly — the
game's own comparison is `limb.name == nm` (<c>Body.cs:1471</c>), not a case-insensitive one.

## Entry mapping

| Entry (CUO gesture, or the game's own) | Native call site it mirrors | What the entry needs that its gesture cannot carry | Verdict |
|---|---|---|---|
| Radial centre drop (inventory → body part) | `PlayerCamera.TryPerformRadialAction` → `Body.WearWearable` | which limb and which slot the garment claims — content data, not gesture data | the item's own declaration answers both; an unplaceable one is refused, never guessed |
| Touch auto-wear as the item is picked up | `Body.AutoPickUpItem` → `Body.WearWearable` | the same | the same |
| CUO's cross-player wear (world drag onto a teammate) | the same native placement, replayed on the owner's client | the same | `IWearSemantics` already asks the item's own data; this ticket makes mod content expressible there |

No entry is added, no new arbitration is introduced, and nothing here is decided by order.

## Mechanism inventory

| # | Mechanism | Evidence | State |
|---|---|---|---|
| 1 | `Wearable` (bool) → `ItemInfo.wearable` | `ModItemInfoFactory.Build`: "wearable = definition.Wearable" | defect: the flag is declarable, the placement is not |
| 2 | `desiredWearLimb` / `wearSlotId` unassigned for mod content | the same build site assigns neither (`ItemInfo.cs:139`, `:142` default null) | defect |
| 3 | The wear family reads that name unguarded | <c>Body.cs:1493-1494</c> (`WearWearable`), <c>:1541</c> (`GetWearable`), <c>:1558</c> and <c>:1575</c> (both `HasWearable`) — the whole `LimbByName` census of the game assembly | defect (the crash, four sites) |
| 4 | The callers those sites are reached from | <c>PlayerCamera.cs:1640-1643</c>, <c>Body.cs:500-528</c>, <c>SaveSystem.cs:338</c>, `RemoteIntentApplier.ApplyWearItem`; the queries from <c>PlayerCamera.cs:1698</c>, the drop report and <c>Body.cs:1358</c> (`DoPickupCheck`) | reachable |
| 5 | CUO's cross-player wear chain | `GameWearPlacement.TryResolve` requires `wearable: true` AND a resolving `desiredWearLimb` | already safe, and blind to mod garments |
| 6 | The remaining wearable fields | the table above (`Limb`, `Body`, `PlayerCamera`, `GroundGlass`) | capability no DTO member carries |
| 7 | The limb-name convention | `IModItemLimbWornSprite.LimbName`, `IModLimbMoodleBinding.LimbName` | already established; reused, not reinvented |

## Plan

1. **Cut the declaration**: `IModItemWearable` / `ModItemWearable` carrying the placement and the fields
   the flow reads, and `IModItemDefinition.Wearable` changes from `bool` to `IModItemWearable?` — the
   family's own shape (`Container` / `Battery` / `Light` / `Tool` / `Gun` / `Visual`), where the behaviour
   travels as one object instead of as a flag beside loose members that must agree with it. Pre-release:
   the breaking change is taken, not avoided.
2. **Materialize it**: `ModItemInfoFactory.Build` maps every member onto the vanilla `ItemInfo`.
3. **A declaration that cannot be placed is inert and reported**: a slice with a blank limb name or a blank
   slot id installs `wearable = false` and the provider reports it at load, naming the definition. That is
   the sibling ticket's ruled shape (materialize-and-report), applied where "materialize" can only mean
   "the item still exists, the unbaked half is off".
4. **Guard the family**: one prefix refuses the wear the native body would dereference null in, and three
   more answer the sibling QUERIES (`GetWearable`, `HasWearable(string)`, `HasWearable(Item)`) with the
   meaning they have for an item that cannot be worn — "not worn" — instead of letting the same null
   through. All four ask the one resolution rule the wear chain reads, and the guard is behaviour-preserving
   for vanilla content, whose wearables always resolve. The wear prefix states its outcome to the postfix
   (`__state`), because Harmony runs the postfix even when a prefix skipped the original and a refused wear
   owes no "inventory changed" snapshot.
5. **Red first**: the construct-level case (the built `ItemInfo` a `Wearable`-declaring definition yields)
   fails on the frozen tree, because it carries `wearable = true` with no placement — the state the game's
   own path dies on.
6. **Whole family**: every field of the table is either mapped or explicitly dropped with a reason; the
   cross-player wear chain's admission of a mod garment is pinned where it is observable; a gate keeps an
   item DTO from declaring a flag whose consumer is content the DTO cannot carry.
7. **Docs**: `docs/en/reference/mod-api.md` with its Chinese pair and the alignment record, the
   `Abstractions` public-surface baseline, the parent ticket's Part 3.A entry, the sibling ticket's
   corrected non-goal, and this ticket's own status.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The declaration (rows 1/2) | `IModItemWearable` / `ModItemWearable` carry the placement and the five statistics; `IModItemDefinition.Wearable` is that slice instead of a `bool`, so the flag cannot travel without the data it gates | the built `ItemInfo` carries all seven values (`ItemWearableDeclarationTests.Item_DeclaringAPlacement_MapsEveryFieldTheGameReadsToPlaceAndWearIt`), and the API baseline moved with two changed signatures |
| The mapping (row 4) | the flag and `desiredWearLimb` / `wearSlotId` / the statistics are installed together, and only for a placeable declaration | red first: `Assert.False() Failure / Expected: False / Actual: True` on the built flag, green after the change |
| The incomplete declaration (rows 3/5) | registered WITHOUT the flag and reported by name at load — the sibling ticket's materialize-and-report ruling, where "materialize" means the item still exists | the same case asserts the warning; the numeric failure path is refused whole (`Item_DeclaringANegativeWearableNumber_IsRefused`) |
| The guard (rows 3/4) | a prefix on `Body.WearWearable` refuses the wear native would dereference null in, before anything is reported, and three more guards answer the sibling queries (`GetWearable`, both `HasWearable`); all four ask the same rule the cross-player chain places with, and the report rides a one-member patch port | `PatchBridgePortShapeGateTests` 22 + `PatchBridgePortContractTests` 13; the rule's name comparison is pinned at L0 (`TheLimbNameRule_MatchesTheGamesOwnComparison`, 6 cases); the native half is an acceptance row (it needs a live `Body`) |
| The refusal's silence (F2 of the review) | the wear postfix no longer reports after a refused prefix: the outcome travels as `WearOutcome`, so no "inventory changed" snapshot is sent for a wear that never happened | the enum carries `Refused` / `WorldItem` / `CarriedItem` and the postfix returns on the first; reviewing the code path is the evidence available without a live `Body` (convention 6: a hook reports only verified writes) |
| The records (row 11) | the ceiling ticket's Part 3.A entry names this ticket and the crash; the sibling ticket's "none of them is a crash" is corrected in place | both diffs, with the native evidence quoted from `Body.cs:1493-1494` |
| Docs (row 10) | both `mod-api` pages carry the row change and the new rule paragraph; the alignment record re-records both hashes | `git hash-object` on both pages equals the recorded pair; the alignment gate is green |

## What landed

The full record is `docs/evidence/selfchecks/mod-api/mod-item-wearable-declaration-selfcheck.md`. In short:

- `IModItemWearable` / `ModItemWearable` (Abstractions) and `IModItemDefinition.Wearable` as that slice; the
  provider installs the wearable flag and the placement together, reports an incomplete declaration at load
  without installing the flag, and refuses negative numbers with the rest of the item's numeric validation.
- `GameWearPlacement` (one limb-name rule, three readers: the cross-player chain, the wear prefix and the
  three query guards) and the guard family over every site the game dereferences the declared limb in —
  `WearWearable`, `GetWearable`, both `HasWearable` overloads — reported through the new
  `IWearablePatchPort`; the wear prefix states its outcome to the postfix so a refused wear reports nothing.
- 10 new cases in `ItemWearableDeclarationTests` (4 on the declaration, 6 on the limb-name rule), 3 new
  port-contract cases, 5 test doubles and the example mod following the contract change; build 0/0, focused
  964/964, behaviour 4913/4913, gates 574/574, format exit 0.
- The two records that were wrong are corrected, and both `mod-api` pages carry the rule.

## Review round (2026-10-11)

The independent FULL-tier review (fresh context, frozen tree; report
`.agent-local/reviews/mod-item-wearable-declaration-review.md`) raised NO blocker, and its three majors were
fixed in this same commit: the crash family was not closed (three more `LimbByName` dereferences, and the
`LimbByName` census is now what bounds it), the refused prefix still let the postfix report an unchanged
inventory, and the guard's rule had no case that could fail. Its minors are fixed too: the caller census
(four, not two), the claim that the guard refuses "exactly" the throwing state (it refuses every unplaceable
wear, which includes states where an earlier native guard would have returned first), the stale sentences in
the two records this change edits, the baseline's decomposition (16 added entries + 2 changed signatures),
the closure count, the abbreviated paths in the self-check, and the red's non-reproducibility from the
committed tree (recorded as a limit).

## Non-goals

- Not the limb action (`usableOnLimb` / `useLimbAction`): the game's own call site runs a delegate, and
  `Abstractions` cannot carry one, so that surface belongs to `mod-authored-effects.md` — exposing the flag
  alone would add a second crash of this ticket's shape.
- Not the miscellaneous Part 3.A reads (`rec`, `onlyHoldInHands`, `combineable`, `ignoreDepression`,
  `scaleWeightWithCondition`, `jumpHeightMultChange`, `slotRotation`): each is its own entry with its own
  consumer, and none is a defect of a promised surface.
- Not a CUO-side limb enum: the vocabulary the API already publishes is the game's own limb name, and a
  second vocabulary would be a second thing to keep in step.
- Not native's drop-the-occupant behaviour on a wear into an occupied slot: the cross-player chain records
  that limit (it needs a world-drop fact this path does not have), and this ticket does not change it.
