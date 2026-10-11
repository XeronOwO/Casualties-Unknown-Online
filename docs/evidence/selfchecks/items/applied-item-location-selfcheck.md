# An applied item keeps its id — stage 1 self-check (2026-10-12)

Ticket: `docs/backlog/todo/item-identity-across-transforms.md`. Design record (the evidence the ruling
rests on): `docs/evidence/selfchecks/items/item-identity-across-transforms-design.md`. **Scope of this
cycle: stage 1 of the ruled option 1 — the model half.** The item row, the wire, the archive and the
projections now carry "an item is applied to a limb" and round-trip it; the PRODUCTION call sites that
create the fact (the adapter's apply and removal commits, and the deletion of the synthesized id
namespace they make possible) are stage 2, and the mod-facing page is stage 3. Nothing in this cycle
changes what a running game does today: no `GameAdapter` path is touched, so an applied row cannot yet
be produced by a real session.

Citations: `reversing/` paths carry line numbers (that tree is never edited); our own `src/` and `docs/`
are cited by path plus quoted text.

## 1. What landed

| Artifact | Change |
|---|---|
| `ItemLocationKind` | `Applied` — the fifth family. An item held by a limb, reached by no inventory gesture. |
| `ItemLocation` | `LimbIndex` (a family-specific field beside `X`/`Y`/`ParentItemId`); `ItemLocation.Applied(owner, limbIndex)`; `NoLimb = -1` for every other family. |
| `ApplyLimbItemCommand` | carried/other live location → applied. Optional payload, as drop and transfer carry it. |
| `RemoveLimbItemCommand` | applied → carried by the operator, and it NAMES the limb it is the counterpart of. |
| `LimbItemLocationRules` | the applied family's rules in one place: its two decisions, plus the two refusals (`RefuseInventoryGesture`, `RefuseAppliedCreation`) that keep its only way in a relocation. Extracted from `ItemDomainModule`, which is at 552 of the 600-line architecture gate. |
| `ItemDomainModule` | dispatch, `CanHandle`, the invariant (an applied row needs both a limb owner and a limb), and the four call sites that refuse a location the family owns: a spawn cannot create an item already on a limb, and a pickup, a drop or a cook cannot move one. |
| `ItemTerminalFact` | the diagnostic comparator's fact gains the limb — two applied items on two limbs agree on every other member. |
| `PlayerDomainModule` | the "item references an unknown player" invariant now covers applied as well as carried (the family alignment). |
| `ItemLocationChain` | documented: applied is NOT world-rooted, so a layer boundary keeps it. |
| `WireItemLocationKind` / `WireItemLocation` | `Applied = 5`; `LimbIndex` as a NULLABLE member. |
| `KernelWireMapper` | both directions; an applied location without a limb is refused rather than read as limb 0. |
| `KernelCommandGateway` | the destroy-eligibility rule names the new family: an applied item's end may be reported by the limb owner alone. |
| `docs/architecture/save-archive-format.md` | §6.1's rule for the applied row. |

## 2. The two architecture points the design record left open

**2.1 A fifth `ItemLocationKind`, not the wear slot encoding.** The wear encoding
(`-(limbIndex + 2)`, `ItemStateCodec.SlotOf`, `IWearSemantics`) is a position INSIDE the inventory: a
worn item is still carried, which is why `IModInventoryEntry` documents a negative `SlotIndex` as "a
worn item" and why a search can reach it. An applied item is the opposite fact — the native apply
destroys the carried `Item` and adds the component (`Item.cs:400-408`, `:1479-1491`, `:1505-1517`), so
nothing in the inventory stands for it, and the ruled model says it is "not in the inventory and not a
wearable" (record §6.2). Reusing the negative slot would have told a mod the player still carries the
splint. The cost of the fifth family is that every consumer of a location has to say what it does with
it; §3 below is that census.

**2.2 The ITEM ROW is the authority for which item is applied — no new field on `ComponentStateMsg`.**
The record's §6.5 states the rule ("the item row says WHICH item is applied and the limb row says the
component is there") and its §7 cost table guessed a second carrier on the limb component
(`ComponentStateMsg`). This cycle does NOT take that second carrier, for the reason the record's own
rule gives: the association is already derivable from the row, because the item's DEFINITION names the
component type (a `splint`/`carcasssplint` row is the `SplintLimb` on that limb, a `tourniquet` row is
the `TourniquetScript`), and the divergence check compares the same three facts either way. A wire field
whose only reader is a stage that has not landed yet is the guess the project's rules forbid
(*A mechanism needs a named consumer*), so it lands in stage 2 with the reader that needs it — or not at
all, if the `(owner, limb, definition)` binding turns out to be enough.

**2.3 The mod surface (settled here, implemented in stage 3).** An applied item is NOT an inventory
entry, so `IModPlayerInventory.Items`/`IModInventoryEntry` must not list it: an entry there means "this
player carries this", which is exactly what became false. The by-id read the ruling promises ("a mod
that recorded the id still finds it") belongs on the state query surface, `IModGameState`, beside
`TryGetPlayer` — the same permission gate, the same point-in-time shape, and a location-shaped answer
(the item is on limb N of player P) instead of an inventory line. Stage 3 writes the lifecycle rule into
`docs/en/reference/mod-api.md` and `docs/zh/reference/mod-api.md` and adds that read with its baseline
entry.

## 3. The consumer census for the new family

Every place that reads a location AND has to decide something about it, and what it does with
`Applied`. A reader whose rule is already "this family only" needs no new branch; it is a row here for
the record (`DecideTransfer`'s `!= Carried`, `RestoredWorldItemSet.Read`'s `World` filter,
`ItemArbitration`'s `!= Carried`) rather than a change. Deliberately NOT rows are the readers that only
pass a location through or filter on it by construction: `GameStateStore`'s working copy and restore,
`ItemKernelAuthority`'s report-drops walk, the restored-id collector, `KernelBatchItemProjection`'s
three REBUILD walks (its batch path is the row below), `ItemStateEquality`,
`GuestCommandReconciliation` (position-independent), and the adapter-side plans that read their own
tables rather than the kernel's location.

| Consumer | Behaviour | Evidence |
|---|---|---|
| `ItemDomainModule.DecideSpawn` | an item cannot be CREATED already installed on a limb: the family has one way in, the relocation that names the limb | `ItemLimbApplicationTests.SpawnDirectlyOntoALimb_IsRejected` |
| `ItemDomainModule.AssertInvariants` | requires a limb owner and a limb | `ItemLimbApplicationTests.AppliedRowWithoutALimb_FailsTheKernelInvariantInsteadOfBeingStored` |
| `PlayerDomainModule.AssertInvariants` | the owner must be a known player (as for carried) | `PlayerDomainKernelTests.AppliedItemWithUnknownOwner_IsRejectedByPlayerInvariant` |
| `ItemLocationChain.IsWorldRooted` | false → a layer-end cut keeps the row and the layer reset keeps it in the kernel | `AppliedItemArchiveTests.LayerEndCut_WritesTheAppliedRowAndStillDropsTheWorldRootedOne`, `ItemLimbApplicationTests.AppliedItem_IsNotWorldRooted_SoTheLayerBoundaryKeepsIt` |
| `WorldSnapshotEncoder.ItemRows` | writes the row | as above |
| `WorldSnapshotDecoder` | `KernelWireMapper.FromWireItem` → `FromWireLocation`; a limb-less applied row is a NAMED per-entry skip | `AppliedItemArchiveTests.AppliedRowWithoutALimb_IsSkippedByNameByTheRestoreInsteadOfLandingOnTheHead` |
| `RestoredWorldItemSet.Read` | filters on `World` → not materialized | `AppliedItemArchiveTests.RestoredAppliedRow_IsNotMaterializedAsAWorldItemAndRaisesNoSpawn` |
| `KernelBatchItemProjection` | no world clone, no carried fact (the peer's clone converges from the 1 Hz character snapshot, as a consumed item's does) | `AppliedItemArchiveTests.ApplyingAnItem_EmitsNoCarriedFactAndNoWorldCloneToAPeer`, with a carried-spawn control on the same seam |
| `ItemArbitration.RebuildCarriedTableFromKernel` | not carried → not in the transfer table (correct: the absolute registration states what the player CARRIES) | covered by the `!= Carried` filter it already applies |
| `KernelCommandGateway.MayReportDestroyed` | only the limb owner may report its end | `KernelCommandGatewayTests.DestroyOfAnotherMembersAppliedItem_IsIgnoredWithoutAnAnswer`, `.DestroyOfAnItemAppliedToTheSendersOwnLimb_IsAdmitted` |
| `ItemDomainModule.DecidePickup` / `DecideDrop` | an applied item is in NO inventory, so both refuse it by name — accepting one would move the item while the component that IS the item stayed on the limb | `ItemLimbApplicationTests.AppliedItem_CannotBePickedUp`, `.AppliedItem_CannotBeDropped` |
| `ItemDomainModule.DecideTransfer` | requires a carried item, so an applied one is already refused | the `!= Carried` rule it has always had |
| `ItemDomainModule.DecideCook` | refuses an applied source by name: an item held by a limb is in no cooker, and the cook's first half would end a row whose component stayed on the limb | `ItemLimbApplicationTests.AppliedItem_CannotBeCooked` |
| `ItemTerminalFact.From` (the diagnostic comparator's fact) | carries the limb, so the comparison can tell two applied items on two limbs of one body apart (they agree on every other member) | `ItemDiagnosticsProjectionTests.Compare_TellsTwoAppliedItemsOnDifferentLimbsApart` |

## 4. Entry mapping and mechanism inventory

No new CUO gesture, UI or player-visible behaviour is introduced by this cycle. The two kernel commands
are the kernel-side counterparts of two native call sites the design record already maps, and neither
has a production caller yet:

| Kernel command | Native call site it will mirror | Evidence |
|---|---|---|
| `ApplyLimbItemCommand` | the item's own `useLimbAction` attaching the component on the affected body (today reached from `PlayerCamera.ApplyWoundItem` for the local body, and from `NativeLimbToolApply` for a cross-player one) | record §2 (four item→component branches, `Item.cs`) |
| `RemoveLimbItemCommand` | `SplintLimb.TakeOff` / `TourniquetScript.TakeOff`, whose callers are `PlayerCamera.cs:781-786` and `:772-776` (the wound view) plus `OnDismember` | record §2's two `TakeOff` call sets |

The rule these commands implement is the ruling's, not this cycle's: applying is a relocation and
removal is a move to the operator, so both keep the instance id. `RemoveLimbItemCommand` therefore names
the limb it is the counterpart of, and a removal that names a different limb (or a different body) is
refused by name (`ItemLimbApplicationTests.Remove_NamingAnotherLimb_IsRejectedAndLeavesTheItemApplied`,
`.Remove_OfAnItemOnAnotherBody_IsRejected`) — the gesture carries the limb, so nothing here falls back to
"whichever limb the id happens to sit on".

## 5. What this cycle does NOT prove

- **No runtime evidence.** Every claim is source-level plus the test suite. A two-client run cannot
  observe the applied location yet, because no adapter path creates one: the cross-player and local
  apply/removal commits, the limb-component binding at the restore seam and the divergence report are
  stage 2, and they are what a real session would exercise.
- **The two carriers' disagreement is not detected.** The item row is the authority for WHICH item is
  applied; the character file's limb row is the authority for the component being there. Nothing reads
  the two together yet, so a disagreement is currently silent — stage 2's, together with
  `todo/id-carriers-can-disagree-about-placement.md`'s neighbouring question, which this answer must not
  contradict.
- **The limb index is a body-layout index with no independent check.** The kernel validates that it is
  present and that the owner is a known player; it does not (and cannot, from the player table alone)
  check that the limb exists on that body's layout. The native guard runs on the side that performs the
  apply, and stage 2 is where the reported index meets the stored limb row.
- **The ice pack, the amputation/dismember entry points, decay-to-zero and death** are the design
  record's own scope notes (§6.4, §8) and are untouched here. Decay-to-zero becoming the item's own
  `Destroyed` end is stage 2's adapter work.
- **The wire checkpoint has a different failure model from the archive.** A malformed applied location
  inside a WIRE checkpoint fails the whole chunk set (`WireCheckpointAssembler.Assemble` routes every row
  through `KernelWireMapper.FromWireItem`, and the receiver logs the failure and drops the set), which is
  what an unknown location kind already does on that path; the per-entry NAMED skip is the ARCHIVE's own
  rule (§6). A limb-less applied row therefore costs a late-joining peer the whole set instead of one
  row — it is re-sent by the repair cycle, and it is a corrupt-writer row rather than a normal outcome.
- **A stale container report can still claim an applied item.** `ItemDomainModule.DecideSyncContainer`
  relocates any child id its report names, so an item that became applied while a container report was in
  flight would be moved back into the container while its component stayed on the limb. The class is not
  new — the same report can equally claim an item another player now carries — and it belongs with stage
  2's divergence rules rather than with a second guard invented here; it is named so stage 2 does not
  have to rediscover it.
- **The pre-release trust model is unchanged, and it is the reason one refusal exists.** A member can
  already report items it does not own (the transport's own comment calls this "the pre-release trust
  model"), so a forged report is not a new class. What IS new is one state a forged report could not
  produce before: an item that exists only as a limb component, whose end only that limb's owner may
  report. `DecideSpawn` therefore refuses an applied location by name — the family's one way in is the
  relocation that names the limb — and the cookie that a forged row would have left (an id that can
  never be reclaimed, and a later real creation refused as `Conflict`) is closed at the door.
- **No protocol number moved**, deliberately: `ProtocolVersion.Current` is frozen at
  `UnreleasedBaseline` before the first release (decision 241), so this wire change is recorded by this
  commit and this page, not by a number.

## 6. Verification as run

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter
  "FullyQualifiedName~ItemLimbApplicationTests|FullyQualifiedName~KernelWireMapperTests|FullyQualifiedName~AppliedItemArchiveTests|FullyQualifiedName~KernelCommandGatewayTests|FullyQualifiedName~PlayerDomainKernelTests|FullyQualifiedName~ItemDiagnosticsProjectionTests"`
  — 93 passed.
- Full: `dotnet test CasualtiesUnknownOnline.slnx` green at gates 615/615 and behaviour 4990/4990 on the
  frozen tree (the cycle adds 35 cases to the 4955 it started from: 19 kernel/limb-application, 7 wire,
  5 archive/restore/projection, 2 admission, 1 player invariant, 1 diagnostic comparator), plus
  `dotnet format` clean. The run that excludes the delivery-checklist gate reports 614 of those 615 —
  that gate is the one the checklist itself satisfies.
- Two independent adversarial reviews ran against frozen trees (`984ffa18` and then `151fd56f`): the first
  found four issues (the diagnostic fact's missing limb, the pickup/drop hole, the wire-checkpoint failure
  model, a stale MANIFEST sentence) and the second found the spawn hole and the stale figures this page
  and the delivery checklist carried. Every one is fixed in this same change.

