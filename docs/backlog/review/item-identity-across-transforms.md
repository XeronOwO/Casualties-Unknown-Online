# An item's identity across a transform: is a worn splint still the item it was made from?

- Status: Review — the RESEARCH cycle landed (design record:
  `docs/evidence/selfchecks/items/item-identity-across-transforms-design.md`) and the user ruled
  **option 1** on 2026-10-11. All three stages are implemented. **Stage 1** (2026-10-12) is the model
  half: the kernel's `Applied` location family, the two limb-application commands and their decisions,
  the wire kind with its required limb, the archive rule and the consumer census, plus 35 cases
  (self-check: `docs/evidence/selfchecks/items/applied-item-location-selfcheck.md`). **Stage 2**
  (2026-10-13) is the adapter half: the apply and removal commits, cross-player and local, the
  limb-component binding, the divergence report at the restore seam, and the deletion of the
  synthesized id namespace with its documentation and test surface (self-check:
  `docs/evidence/selfchecks/items/applied-item-round-trip-selfcheck.md`). **Stage 3** (2026-10-14) is
  the mod-facing half: the lifecycle rule written into `docs/en/reference/mod-api.md` and its Chinese
  twin, and the by-id read `IModGameState.TryGetItem` with its reviewed baseline entry and 8 cases
  (self-check: `docs/evidence/selfchecks/items/applied-item-mod-surface-selfcheck.md`). Decision 259.
  The rule is true in a running game and a mod author has an official way to read it. What remains is
  acceptance, not code: no two-client run has observed the round trip, and the adapter's Unity-facing
  decision code (`LimbItemTransform`) has no case that runs without the game. The draft below is the
  frozen design it implements.
- Priority: High
- Category: Item identity / model contract / mod API / persistence
- Source: handoff instruction (2026-10-11) after `review/id-resume-sources-still-unseeded.md` landed:
  "一件物品经过『变换』（装到人身上、烤肉、制作……）之后，还算不算同一件物品；CUO 要给模组作者一条统一答案."
  It is the same family as the id-resume work — that cycle fixed "one number never lands on two objects";
  this one asks what the number MEANS once the object it was stamped on stops existing.
- Related: `review/id-resume-sources-still-unseeded.md` (the counter and its two carriers),
  `todo/id-carriers-can-disagree-about-placement.md` (the two carriers disagreeing about PLACEMENT),
  `src/CasualtiesUnknownOnline.GameAdapter/Character/LimbComponentStateCodec.cs` (the cross-player
  carrier for limb-mounted component state), `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalRemovalApplier.cs`
  (the removal effect that mints a synthesized id today),
  `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemLocationKind.cs` (the five location families),
  `docs/architecture/save-archive-format.md` §3.4/§6.1, `docs/en/reference/mod-api.md`
  (`IModInventoryEntry` — the mod-visible half).

## The question

CUO gives every runtime item an instance id and publishes it to mods (`IModInventoryEntry`: "The
framework item-instance id (0 when not yet allocated)"). It never says what that id means after the item
stops being an item. A splint applied to a limb is the clearest case: the game destroys the carried
`Item` and adds a `SplintLimb` component to the `Limb` (`Item.cs:1484-1489`), and taking it off
instantiates a NEW `Item` that copies `condition` and nothing else (`SplintLimb.cs:19-26`). A mod that
recorded the id has no stated answer for "the splint I recorded is now on someone's leg" — and neither
does CUO's own favourite-mark ledger, which is keyed by that id.

## What the research established

The full evidence is in the design record; the load-bearing findings are:

1. **The native family is exactly `SplintLimb` (two definitions) and `TourniquetScript` (one).** The
   game itself says so: `Limb.cs:101-103` dispatches `OnDismember` by `SendMessage`, and only those two
   types declare it. `ChilledLimb` is NOT one of them — the ice pack survives the apply
   (`destroyAtZeroCondition = false`) and the component reads no item field at all.
2. **The boundary is "does the game turn this component back into an item".** Splint and tourniquet:
   yes, and the round trip carries `condition`. Ice pack, every other limb tool, cooking and crafting:
   no.
3. **No component carries any identity anywhere** — not on the scene object, not on the wire
   (`ComponentStateMsg` is `{TypeName, Fields}`), not in the archive. That is the gap every option
   below has to fill first.
4. **CUO is not consistent about the round trip today.** The same native transform is recorded as two
   different terminal kinds (`Consumed` cross-player via `PlayerItemUseCommit.DestroyKernelCarriedItemIfPresent`,
   `Destroyed` when the local body ran it natively), and removal produces a new id through TWO different
   counters: a cross-player removal mints from the synthesized namespace, while a LOCAL removal lets the
   native `TakeOff` run un-intercepted (`RemoteMedicalPatches` only blocks while the remote medical view
   is open) and the plain allocator stamps the object it instantiates.
5. **Removal carries the definition and `condition`, and nothing else.**
   `OtherMedicalRemovalApplier.GrantRemovedItemToOperator` mints a new id from the synthesized
   `AwardedHighBit` namespace and copies the splint's `item` template (or the hardcoded `"tourniquet"`)
   plus `condition`; the tourniquet's `timeApplied` is dropped because nothing else in `src/` reads it.
6. **CUO already has a positional model for "an item on a body" that keeps its id**: the negative wear
   slot `-(limbIndex + 2)` (`ItemStateCodec.SlotOf`, `IWearSemantics`, `ItemData.SlotIndex`). The
   question is not whether a body position can be expressed — it is whether an applied component gets
   one.

## The decision draft (RULED 2026-10-11: option 1)

**What should CUO tell a mod author about an item that is applied to a limb?** The user ruled
**option 1** — an applied item keeps its instance id and gains a location on that limb.

| # | The answer | What a player and a mod author see |
|---|---|---|
| 1 | **The item keeps its id and gains a location on that limb** (recommended) | A splint is the same splint on the leg and off it. A mod that recorded the id still finds it; a favourite mark still resolves; taking it off is a transfer to the operator, not a new item. Costs the widest change: a new kernel location family, a wire field, and the capture/restore cluster. |
| 2 | **The item dies; the item that comes back carries a `derivedFrom` pointer** | A mod sees "that id is gone, here is its child". Needs the same component-carries-the-id step as option 1 (there is nothing to derive from otherwise), then saves the kernel/wire half — and buys a weaker answer. Chains and a dead parent need their own rules. |
| 3 | **Document the status quo: an id names one incarnation, and a transform ends it** | Nothing changes in code; one page states the rule. A mod (and the favourite ledger) cannot follow an item across the most common transform a player performs on somebody else's body. |

**The recommendation was option 1**, for three reasons:

- The user's stated leaning is positional, and the tree already agrees with it: the wear encoding
  already gives "an item on a body" a position and keeps its id, so option 1 extends a rule that exists
  rather than inventing one.
- It closes a real inconsistency in the same work: with the applied item no longer terminal, the
  cross-player and local applies converge on one commit and the local removal stops minting through the
  plain counter. That convergence is a cost item in the design record's §7, not a free side effect.
- It DELETES a mechanism instead of adding one at the removal end: the synthesized-id namespace
  (`ItemIdCoordinator.NextAwardedItemId`, `ItemInstanceIds.AwardedHighBit`) loses its only production
  consumer and goes with it, and with it the counter, its resume point and the `ResumeAwardedIds` half
  of the restore — plus the format-page paragraph, the sync-coverage row and the tests that pin them.
  The id-resume cycle's newest machinery shrinks rather than grows.

**What option 1 does not decide, and the implementing cycle settled:** the applied location is a FIFTH
`ItemLocationKind`, not the wear slot encoding — a worn item is CARRIED and a search reaches it, while
the native apply destroyed the carried object, so reusing the negative slot would tell a mod the player
still carries the splint. The ITEM ROW is the authority for WHICH item is applied: `ComponentStateMsg`
gains no id carrier, because `(owner, limb, definition)` already binds the row to the limb's component
(the definition is what separates a splint from a tourniquet on one limb). And the mod surface does NOT
list an applied item as an inventory entry: stage 3 adds the by-id read on `IModGameState` and the
written lifecycle rule. The reasons and the evidence are in the stage-1 self-check's §2.

## Limits

- Runtime evidence: every claim is source-level plus the test suite. The stage-2 self-check's §5 names
  what is still unproven, including the one race the local removal has (a take-off within one round trip
  of the apply, before the applying client's own kernel holds the applied row).
- The ice pack's spent charge is out of this rule's scope: branch 4 spends `item.condition` with no
  floor check and the item survives.
- Two limits stage 1 named, and what stage 2 did with each, both in the stage-1 self-check's §5: a wire
  checkpoint whose applied row names no limb still fails the WHOLE chunk set (the model every unknown
  location kind already has on that path, while the archive skips the row by name), and a container
  report that was in flight when the item became applied can still claim it back into the container
  (`DecideSyncContainer` relocates any child id it names). The divergence rules landed at the restore
  seam and at both removal gestures; this in-flight claiming class stays open with
  `todo/id-carriers-can-disagree-about-placement.md`.
- `Limb.cs:135-138` (the severed limb's wearables are dropped first at `Limb.cs:104-107` and then
  destroyed; whether the destroy still reaches anything is not established by reading) is recorded as
  outside this rule, not as covered by it.
- The two carriers disagreeing about placement stays `todo/id-carriers-can-disagree-about-placement.md`'s
  question; this answer must not contradict it.
