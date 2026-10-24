# An item's identity across a transform: is a worn splint still the item it was made from?

- Status: Done — all seven acceptance rows pass on the fixed artifact (`0.1.0+ef05371c`); record:
  `docs/evidence/acceptance/item-identity-across-transforms-20261018-v.md`. The "Fix landed" section names
  the two causes the earlier batch found, the third finding the review added, and the named limits; the
  previous batch's rejection is `docs/evidence/acceptance/item-identity-across-transforms-20261018.md`. The
  RESEARCH cycle
  and all three implementation stages landed (2026-10-12 → 2026-10-14) and the user ruled **option 1** on
  2026-10-11: stage 1 is the kernel model (`docs/evidence/selfchecks/items/applied-item-location-selfcheck.md`),
  stage 2 the adapter commits and the deletion of the synthesized id namespace
  (`docs/evidence/selfchecks/items/applied-item-round-trip-selfcheck.md`), stage 3 the mod-facing rule and
  `IModGameState.TryGetItem` with its baseline entry and 8 cases
  (`docs/evidence/selfchecks/items/applied-item-mod-surface-selfcheck.md`); the design record is
  `docs/evidence/selfchecks/items/item-identity-across-transforms-design.md` and decision 259 records the
  ruling. The draft below is the frozen design the implementation follows.
- Priority: High
- Category: Item identity / model contract / mod API / persistence
- Source: handoff instruction (2026-10-11) after `done/id-resume-sources-still-unseeded.md` landed:
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

## Fix landed (2026-10-18), before the acceptance re-run

Both defects the batch recorded are fixed at their cause, and neither needed a wider guard.

**1 — the owner's destroy is not the item's end (I1).** The host's limb-component outcome now marks the
result it publishes: `PlayerItemUseResultMsg.BecameLimbComponent` → `RecordPlayerItemUseResultCommand` /
`PlayerItemUseResultEvent` → wire payload field 27 → `PlayerInteractionKernelCodec`. The owner's client
reads that flag in `PlayerInteractionApply.OnPlayerItemUseReceived` and attaches the SAME
`LimbAppliedItemOrigin` marker the local apply seam attaches before destroying its own carried object, so
`ItemPatches.ItemOnDestroyPatch` stays silent and the row committed as `Applied` is no longer overwritten
21 ms later. The marker was already the family's answer; what was missing was the fact that made it
reachable from the cross-player side. A guest owner gets the same answer through the wire field, so the
two directions (host applies to a member, member applies to the host) share one path.

**2 — the take-off's own pickup is the removal, not a second report (I4).** `LimbItemTransform` opens
`CallContext.Origin.LimbComponentTakeOff` around the native `TakeOff` — only when the applied row
resolved — and the `Body.PickUpItem` and `Container.LoadItem` report hooks stay silent inside it: the
item the component revives comes back through the removal relocation, which is the fact the seam already
reports. A take-off with no applied row opens no scope; the item it creates is new and its pickup is a
real fact. The refusal half is routed in `ItemCommandRejectionRouter`: a refused `ItemApplyLimb` /
`ItemRemoveLimb` is a divergence (the native action already ran) or a command the host does not hold, and
it is NAMED, never answered with the pickup rollback that unparented the live object into the world for
the reconcile to kill (batch `20261018-u`'s I4).

**3 — the removal's row carries the revived object, not the pre-apply one.** The review of this fix
found that `ItemMessageFlowService.SendItemRemovedFromLimb` sent no data, so `LimbItemLocationRules.DecideRemove`
kept the applied row's data: its slot was where the item used to be BEFORE the apply and its condition was
the last applied-condition step — and nothing later updates that slot. `LimbItemTransform` now captures the
revived object after the game's own `AutoPickUpItem` placed it (slot or limb encoding, -1 for a container or
the world) and the report carries it, on both the host's own path and the member's wire command.

**The red** has two halves and they are different failure shapes. The runtime red is the batch's own I1/I4
reading on the deployed artifact (`+a3dff483`): the applied row went `Terminal` 21 ms after the commit, and
the taken-off item's local copy was rolled out and killed. The unit red is the refusal-settlement pair in
`ItemArbitrationFlowTests` (`RefusedLimbRemoval_…`, `RefusedLimbInstall_…`): both failed on the pre-fix
router, which surfaced the pickup rollback's item reject for the limb commands, and both pass with the
branch. The kernel/wire round trip
(`PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerItemUseResultEvent`), the limb-tool outcome
(`MedicalToolApplicationTests.AffectedSideOutcome_ComponentToolThatBecameTheComponent_…`), the two
relocation-data cases (`LimbItemTransformSessionTests`) and a source-shape gate
(`LimbToolChainGateTests.TheAppliedFamily_ReportsOneRelocationPerTransformOnBothSides`, which checks the
marker's condition and order, the scope's open/close points and both hooks' guard-before-report ordering —
not just that the names appear) pin the wiring. That gate was itself mutation-tested: the review's four
counterexamples — an unconditional marker, the scope opened above the applied-row refusal, a deleted
postfix release, and a hook guard moved below its report — were each applied to the tree and each failed
the gate with its own message. The sync-coverage matrix's P8 row declares wire payload field 27 with its
own anchor; `PlayerItemUseResultMsg`'s field 16 is process-local metadata on the same fact, not a second
wire member.

### Self-check: mechanism × change × evidence

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | The host's limb-component outcome | the result carries `BecameLimbComponent`, and every layer (command, event, wire payload 27, codec) passes it | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerItemUseResultEvent`; `MedicalToolApplicationTests.AffectedSideOutcome_ComponentToolThatBecameTheComponent_…` (the wire-decoded result) |
| 2 | The owner's client (both directions: host owner and guest owner) | the marker `LimbAppliedItemOrigin` is attached inside the flag before the deferred destroy | the same two cases plus `LimbToolChainGateTests.TheAppliedFamily_…` (condition + order); row I1 |
| 3 | The item's own `OnDestroy` hook | the existing marker read is unchanged and now reachable from the cross-player side | the same gate; `ItemPatches.ItemOnDestroyPatch` |
| 4 | `SplintLimb.TakeOff` / `TourniquetScript.TakeOff` | the scope `CallContext.Origin.LimbComponentTakeOff` is opened only after the applied row resolves | `LimbToolChainGateTests.TheAppliedFamily_…` (the ordering check, mutation-proven); rows I2/I4 |
| 5 | `Body.PickUpItem` / `Container.LoadItem` reports | both hooks return before reporting inside the scope | the same gate (both patches, guard before report); row I4 |
| 6 | A refused limb relocation | `ItemCommandRejectionRouter` names it and never surfaces the pickup rollback | `ItemArbitrationFlowTests.RefusedLimbRemoval_…` / `RefusedLimbInstall_…` (red before the branch) |
| 7 | The removal relocation's data | the revived object's slot and condition ride `SendItemRemovedFromLimb` | `LimbItemTransformSessionTests` (host path and guest wire path) |
| 8 | A self-contradictory outcome | `BecameLimbComponent` without `Consumed` is refused by name | `MedicalToolApplicationTests.AffectedSideOutcome_ClaimsTheComponentWithoutConsumingTheItem_IsRefused` (red before the guard) |
| 9 | The wire and its bookkeeping | no new discriminator, `ProtocolVersion` untouched; P8 declares field 27 with a new anchor | `SyncCoverageGateTests`; the matrix row and `sync-coverage-evidence.json` |

**Limits this fix does not close, named rather than papered over.** A take-off whose native
`AutoPickUpItem` cannot place the item (no empty slot and no surface container) leaves the object in the
world while the row relocates to carried — the kernel has no Applied → World door, so that landing is the
family's divergence; the batch did not observe it. A take-off whose item lands in a surface container
carries slot -1 and no parent: the kernel's removal door is Carried-only, so the row stays a top-level
carried item and the containment lives only in the character snapshot and the transfer table (the 1 Hz
fallback carries the clone and the restore, not the kernel row). The take-off scope covers the two landing
shapes the native templates can take; a modded component whose item is `Stats.wearable` or carries
`noautopickup` would land through a path the scope does not silence (the native splint and tourniquet
templates are neither), and a `CallContext` depth-bound refusal at 16 would silently disable the guard —
the frame boundary reports that refusal rather than repairing it. And the two-carriers placement question
stays `todo/id-carriers-can-disagree-about-placement.md`'s.

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
