# Cross-player topical semantics from the game's own data self-check

Owner cycle: `todo/mod-cross-player-native-semantics.md` Part B, topical chain (cut
out of `todo/mod-content-ceiling.md` Part 2 stage 2). Decision: the topical chain
stops carrying an id/coefficient table — the game's own registries answer WHO may
be treated, the operator's client runs the item's own native limb action to
MEASURE the dose, and the patient's client runs the game's own `ApplyToLimb`
effect on its own body. The host keeps the allowed operation, the resource and the
arbitration. Same shape as Part A (injection), one chain per deliverable.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The native topical implementation | `WaterContainerItem.cs:218-234` `ApplyToLimb`: `CalculateDrain` (proportional, capped) → per stack `if (liquidType.healthUsable) onHealthUse(ml, limb)` → `Drain`. No sickness term, no second gate |
| 2 | The per-item amount is not data | `Item.cs:650-653` paincream `ApplyToLimb(limb, 10f)`, `:674-677` woundglue `20f`, `:2097-2102` disinfectant `10f`, `:2121-2126` spraybottle `10f` — `ldc.r4` literals inside each delegate |
| 3 | The item-level clip is in the delegate | `Item.cs:2100` / `:2124` play `Sound.Play("spray", limb.transform.position, …)` before the container call; paincream and woundglue play nothing there |
| 4 | The liquid-level clip is in `onHealthUse` | `Liquids.cs:1074` reliefcream and `:1100` woundglue play `Sound.Play("cream", limb.body.transform.position, …)`; alcohol, bleach, disinfectant and soap play nothing |
| 5 | The content facts | `Item.cs:7168` `Item.GlobalItems` (a `LiquidItemInfo` with `usableOnLimb`), `Liquids.cs:1846` `Liquids.Registry` (`LiquidType.healthUsable`) |
| 6 | What was deleted | `RemoteTopicalCatalog` (4 item ids, 6 liquids with per-ml coefficients), `RemoteTopicalApplication`, `RemoteTopicalLiquidEffect`; the treatment table's four topical rows (`TreatmentClips` spray ×2, `TreatmentLiquidClips` cream ×2, `LiquidDrivenItems` ×2) |
| 7 | What replaced it | Runtime: the new `ILimbUseSemantics.IsHealthUsableLiquid` predicate, `TopicalAdmission` (the one admission rule), `CarriedItemUseTree` (the recursive carried-item tree, split out of `PlayerItemUseService` at the architecture gate's 600-line line). Adapter: `GameLimbUseFacts.IsHealthUsableLiquid`, `RemoteTopicalUseHandler` (the measurement), `Patches/RemoteTopicalPatches.cs` (the divert), `NativeTopicalApply` (the effect), `NativeLimbTarget` (the limb rule, now shared with `NativeInjectionApply`) |
| 8 | The wire | `PlayerItemUseRequestMsg.DoseMl` (field 4) carries the measured ml to the host; `PlayerItemUseResultMsg.AppliedDose` (field 11) and `LimbSelection` (field 12) carry the committed drain and the operator's limb to the patient, journaled as `PlayerItemUseResultEvent.AppliedDose` / `.LimbIndex` and `WirePlayerInteraction.AppliedDose` (21) / `LimbSelection` (22) |
| 9 | The held-remote-item entry needs no new field | the dose rides the existing `RemoteInventoryIntentMsg.Amount`, which already carries "the ml the native call moved" for the drain tick |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| Host admission | `TopicalAdmission` asks the game's data (limb-drawable container + one `healthUsable` stack); `CarriedItemUseTree.IsActuallyUsable` uses the same rule, so the auto-select half cannot drift from the explicit-instance half |
| Host drain | `PlayerItemUseService` keeps `LiquidDrainPlan` and the committed-ml arithmetic and writes NO target state: the topical branch sets `appliedDose`, skips `SaveCharacterData(target, …)` and publishes `Health = null` / `Limbs = []` |
| Patient apply | `PlayerInteractionApply` runs `NativeTopicalApply` for a message that carries a dose; the call is deliberately OUTSIDE the `RemoteApply` scope so the liquid's own clip is relayed (see the limits) |
| Operator entry — medical view | `RemoteMedicalOperationHandler.TryStartRemoteTopicalUse` runs the item's own limb action on the displayed limb through `RemoteTopicalUseHandler.TryMeasure`, returns `DispatchedNative` (which suppresses the table's clip replay) and sends the measured dose |
| Operator entry — world drag | `CrossPlayerDragUse` measures on the remote player's own render clone (falling back to `PlayerCamera.selectedLimb`) and keeps the limb hint at -1, so the patient still auto-picks on its own body exactly as before |
| Operator entry — held remote item | `RemoteDragMutationPatches.RemoteDragApplyWoundItemPatch` measures the dragged proxy on the requester's own selected limb — the limb the replaced native call reads — and the amount rides the existing intent field |
| Sound | Both halves of a topical container's clip are native now (item-level from the operator's measurement window, liquid-level from the patient's apply), so the four topical treatment-table rows were removed and the gate proves the split in both directions |
| Sibling chains | Consume/drink, drink medicine, wear, limb tool, bandage, shrapnel and the other-medical sets are untouched; their tables still answer for them and are Part B's remaining work |
| Tests/tools/docs | The deleted catalog's 7 cases are replaced by `TopicalSemanticsTests` (9) and the three `ItemUseTests` topical cases are rewritten to pin the dose and the absence of host-computed state; the harness fake gained the four topical containers and the six `healthUsable` liquids; the two gates that read the deleted source (sound census, patch-bridge census) were reviewed and updated |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| Admission from the game's data | `TopicalSemanticsTests.Admission_AcceptsALimbUsableContainerWithAHealthUsableStack`, `..._AcceptsAMixedContainerTheOldCatalogRefused`, `..._RefusesALimbUsableContainerWithNoHealthUsableLiquid`, `..._RefusesAnItemTheGameDoesNotDrawFromALimb`, `..._RefusesAnEmptyContainer` |
| The two rules decline each other's family, and the routing order is pinned where it is observable | `TopicalSemanticsTests.TheTwoRulesAreDisjointOnTheContentThisSuiteModels` (the FAKE's content only — see its doc comment) and `ItemUseTests.Use_AContainerHoldingBothKinds_IsRefusedByTheInjectionFirstOrder` (the production one-shot chain's order) |
| The host draws the measured dose, and only the measured dose | `TopicalSemanticsTests.TheHostDrawsTheDoseTheItemsOwnDelegateComputed`, `..._CapsTheDoseAtWhatTheItemReallyCarries`, `..._RefusesAUseWhoseGestureMeasuredNothing` |
| The dose reaches the patient and the host invents no body state (rewritten from the case that pinned the host's own per-ml math) | `ItemUseTests.Guest_UsesPaincreamOnHost_CommitsTheDoseAndCarriesItToThePatient` (asserts the single dose, `Health is null`, `Limbs` empty, and the host's own limb snapshot still at 0) |
| The limb selection reaches the patient instead of being resolved on the host (rewritten from the auto-pick case) | `ItemUseTests.Guest_UsesTopicalOnSelectedLimb_CarriesTheSelectionToThePatient` |
| A request with no measured dose is refused and the item keeps its liquid | `ItemUseTests.Use_ATopicalRequestWithNoMeasuredDose_IsRefused` |
| A liquid no chain claims is refused by the registries, not by an allowlist (rewritten from the deleted catalog's unknown-liquid case) | `ItemUseTests.Use_ALiquidNoChainClaims_IsRefused` |
| The journal round-trips the dose and the limb | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerItemUseResultEvent` (extended) |
| The native half exists and cannot double-play | `ItemAndBodySoundCaptureGateTests.TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem` (table-decided vs native-decided in both directions, with the new handler/patch pins) and `RemoteMedicalTreatmentSoundCatalogTests.ATopicalContainer_LeavesBothClipsToTheNativeCalls` (that test pins the TABLE's silence; the gate pins the native deciders' existence) |
| The seam does not widen the patch bridge | `PatchBridgePortShapeGateTests` (two new members pinned in the medical port and in the aggregate's surface) |

## 4. Verification design and result

- Focused: `dotnet test CasualtiesUnknownOnline.slnx --filter
  "FullyQualifiedName~TopicalSemanticsTests|FullyQualifiedName~ItemUseTests|FullyQualifiedName~RemoteMedicalTreatmentSoundCatalogTests|FullyQualifiedName~InjectionSemanticsTests|FullyQualifiedName~PlayerDomainKernelTests|FullyQualifiedName~ItemAndBodySoundCaptureGateTests"`
  — green (95 behaviour + 29 gate cases).
- Full with build: behaviour **4786/4786** (net48) and gates **481/481** (net8.0),
  one `dotnet test` run per project, no `--no-build` on either. (The gate project
  reads 480/481 while this cycle's own delivery-checklist box is still unchecked;
  that is the gate working, and the 481/481 here is the run after it was ticked.)
- `ProtocolVersion.Current` is NOT bumped and no new wire ID was added: the field
  is still the frozen pre-release baseline and the three added members
  (`PlayerItemUseRequestMsg.DoseMl`, `PlayerItemUseResultMsg.AppliedDose` and its
  `LimbSelection`) sit inside messages that already exist. The compatibility
  boundary stays the handshake.
- Coverage direction, stated rather than implied: every L0 topical case here is the
  GUEST-operator direction (a guest uses its own item on the host). The
  host-operator direction shares the same host entry (`HandleUseRequest` with the
  local SteamId) and the same patient apply, but no case drives it, and no case
  drives a third peer. Both are acceptance-batch rows in the ticket.
- The native half is NOT verifiable in L0: `NativeTopicalApply`, the operator's
  native limb action, the `ApplyToLimb` divert and the clip relay need a game
  process. The acceptance rows for them belong to a later agent acceptance batch;
  the L0 half pins the wire, the arithmetic and the verdict, and the gate pins
  that the native half still exists.
- Process note: the independent review ran against a tree that moved once under
  it — this cycle's delivery-checklist rewrite landed mid-review, which left the
  gate project at 480/481 on that gate's own pending box. Nothing under `src/` or
  `tests/` moved (the reviewer checked every path's mtime), so its source and test
  conclusions stand, and the suite totals above were re-established on the FINAL
  tree after its findings were fixed. The checklist box is ticked after that run,
  which is why the earlier 481/481 reading is not the one recorded here.

## 5. Limits recorded

- **Sound is the one place this chain deliberately differs from Part A.** The
  character-sound capture refuses to relay anything played inside the
  `RemoteApply` scope (`SoundPlayPatch`: `if (!CallContext.IsWithin(Origin.RemoteApply))`),
  which is why the injection chain's delegate clips stay on the patient. A topical
  container's liquid clip comes from `onHealthUse`, so keeping it inside that scope
  would have removed a clip every peer hears today. `PlayerInteractionApply`
  therefore runs `NativeTopicalApply` outside the `RemoteApply` scope, inside its
  own `CharacterMedicalUse` scope, and the clip is relayed from the patient
  exactly as it is for a local application. Only `Sound.Play` in this handler keys
  off that scope (checked: `SetDisinfect` and `CoUtils.DoTimedOp` are not hooked,
  and no capture patch reacts to a limb field write), so nothing else changes.
  Part A's equivalent tail is left as it stands.
- A topical container's dose is measured by ONE synchronous native call. A delegate
  that deferred the container call (started a minigame) would deliver nothing
  inside the window and the gesture would be refused by name rather than reported
  as a zero dose. That is unreachable today: a topical container is a vanilla
  `LiquidItemInfo` whose delegate is one `ApplyToLimb` call, and the mod API cannot
  author a limb action at all (`ModItemDefinition` has no limb-use behaviour —
  Part 3 A of the ceiling ticket).
- The routing predicate reads the LIQUID's flags, while the native dispatch reads
  the ITEM's delegate. A container holding a `healthUsable` liquid whose delegate
  called `Inject` instead would be admitted here, measure nothing (the divert only
  owns `ApplyToLimb`), and be refused — with the native `Inject` having already run
  locally. Vanilla has no such item (the four topical containers call `ApplyToLimb`
  and the injectable ones hold `injectable` liquids), and mod content cannot author
  one yet. The refusal is logged, and no host drain is committed.
- The world-drag entry has no limb of its own, so it measures on the remote
  player's render clone's most-injured attached limb — the same pick the host used
  to make — and falls back to `PlayerCamera.selectedLimb` when the clone is not
  rendered yet. The limb hint sent to the host stays -1, so the patient still
  resolves the treated limb on its own body. The amount is limb-independent for
  every delegate that can reach this chain today.
- The held-remote-item entry measures the dragged DISPLAY PROXY on the requester's
  own selected limb, which is the limb the replaced `PlayerCamera.ApplyWoundItem`
  reads; only the amount travels, so the proxy is never mutated.
- A dose the patient cannot apply (a liquid the registry does not know, a
  `healthUsable` liquid with no delegate, or a body with no usable limb) is logged
  and skipped AFTER the host has already committed the ml and drained the item, so
  the resource is spent without an effect. Native `ApplyToLimb` would throw on the
  delegate-less liquid; this path does not take the patient's client down with it.
- The result event is journaled, so a missed-range re-delivery reaches the patient
  again; `ItemKernelAuthority.Apply` dedupes by operation id, so the ordinary
  replay cannot double-apply the dose. The independent review could NOT falsify the
  remaining window but showed the code admits one: `Restore` clears that set, and
  `KernelProtocolService.ApplyBatchAndDrain` only drops batches at or below the
  restored revision, so a post-restore gap replays batches ABOVE it that this
  client may already have applied. Stated precisely, the limit is "not reachable
  from any current sender unless a mid-session restore rolls the kernel back behind
  batches already applied", and only a real session can settle it. The exposure is
  the family's rather than this chain's — every other effect it carries is
  idempotent, so a dose is the first member for which that matters.
- The dose's VALUE is the operator's own client assertion and the host takes it on
  trust: what is captured is the amount argument the item's delegate passed, not
  what the container holds or would drain, and for this family it is a per-item
  constant (10 or 20). The truth-preserving step is the host's own cap —
  `LiquidDrainPlan.TryCreate` draws `Math.Min(amount, total)` from the
  authoritative stacks — so a lying client can ask for no more liquid than the item
  really carries, but it does choose the amount. Part A's limits discuss the dose
  CALL COUNT and not the value; this is the same trust boundary on the other
  family, recorded rather than implied.
- **One user-visible eligibility change is deliberate and worth naming.** The world
  drag's `IsUseItem` used to refuse a topical bottle whose liquid was not one of the
  deleted table's six, and the generic tail that followed would otherwise have
  accepted it as a DRINK; with the table gone, that bottle now falls through to the
  drink branch (`RemoteConsumeCatalog.IsKnownLiquid`), so dragging, say, a
  water-filled spraybottle onto a teammate feeds them instead of dropping the item.
  That is the HOST's own behaviour for any water container and was already the
  behaviour for a syringe of water under the old gate — the id table was the
  anomaly, not the rule — so the operator gate now agrees with the chain it feeds.
  No test pinned the old asymmetry, and no limit elsewhere in the repository
  mentions it.
- The transcribed table this replaces was not merely slower to maintain, it was
  wrong outside the exact vanilla dose: `woundglue`'s native
  `limb.pain *= 0.9f * Clamp01(ml / 20f)` (`Liquids.cs:1106`) was modelled as
  `1 + (0.9 - 1) * min(1, ml / 20)`, which agrees at 20 ml and gives 0.95 where the
  game gives 0.45 at 10 ml; `disinfectant`'s `SetDisinfect(Mathf.Min(240f * ml / 10f, 240f))`
  and alcohol's, bleach's and reliefcream's `SetDisinfect` caps were all modelled as
  uncapped per-ml products; and reliefcream's timed pain ramp
  (`Liquids.cs:1069-1072`) was missing altogether. No test pinned those numbers
  (see §6), so the migration removes the class of error rather than a pinned case.
- `PlayerItemUseService` crossed the architecture gate's 600-line limit while this
  chain was being written — the gate reported **619** aggregate lines at that point,
  an IN-SESSION INTERMEDIATE reading that is not reproducible from this tree (HEAD
  is 580, the final file is 489 and the extracted `CarriedItemUseTree` is 169). The
  split landed in the same change rather than a `docs/architecture-debt.json`
  entry, so the final tree is inside the limit without one.
- The treatment-table census floors were re-reviewed, not lowered to fit: the
  clipped set fell 11 → 9 and the table-decided set 34 → 30 because four ids left
  the table for a native decider, and both floors were set to the new measured
  counts while `accepted >= 45` was left where it was — the accepted surface is
  unchanged, because the four topical ids moved from a file the gate read to a
  pinned census array (`VanillaTopicalCarriers`, the same shape
  `VanillaInjectableCarriers` already uses). The independent review reproduced all
  three counts from the tree.

## 6. Disposition of the deleted catalog's seven cases

The deleted `RemoteTopicalApplicationTests` is listed case by case, because "no
case was dropped" is not true as a blanket statement — the effect arithmetic it
pinned no longer exists anywhere, so three cases have no successor by
construction and are named as such rather than counted as replaced.

| Deleted case | Disposition |
|---|---|
| `Catalog_ExposesKnownTopicalItemsAndLiquids` | Superseded, in a stronger form: the table's census is now the game's own registries, pinned by `TopicalSemanticsTests.Admission_*` and by the gate's hand-written `VanillaTopicalCarriers`. |
| `Plan_DrawsItemAmountOrEntireSmallStack` | Replaced, in a stronger form: the per-item amount is no longer a constant, so `TopicalSemanticsTests.TheHostDrawsTheDoseTheItemsOwnDelegateComputed` and `..._CapsTheDoseAtWhatTheItemReallyCarries` pin the measured dose and the cap instead, and `InjectionSemanticsTests`' three `DrainPlan_*` cases pin the proportional draw that both chains share. |
| `Plan_RefusesUnknownLiquidEvenForKnownItem` | Replaced: `ItemUseTests.Use_ALiquidNoChainClaims_IsRefused` pins the registries' verdict on a liquid no chain claims, which is a stronger statement than an allowlist miss. |
| `ApplyWoundglue_AppliesLimbAndBodyEffects` | **No successor — dropped because native.** It pinned CUO's transcription of `Liquids.cs:1097-1109`; that arithmetic is now the game's own delegate, executed on the patient's client, and no L0 case can reach it (it needs the game's `Limb`). Its numbers were also wrong outside the exact vanilla dose (§5). |
| `ApplyDisinfectant_UsesMaxNotAdditionForDisinfection` | **No successor — dropped because native.** The set-value rule is `Limb.SetDisinfect` inside the game's delegate; the same reason as the row above. |
| `ApplyReliefcream_RequestedLimbWinsOverMostInjuredAutoPick` | Superseded, in a stronger form: the rule moved to the affected side and is shared with the injection chain, pinned by `InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured`, and the operator's pick is carried rather than resolved by `ItemUseTests.Guest_UsesTopicalOnSelectedLimb_CarriesTheSelectionToThePatient`. |
| `ApplySoap_ReducesDirtynessAndSetsShortDisinfection` | **No successor — dropped because native.** Same reason as the woundglue and disinfectant rows. |

The three `ItemUseTests` topical cases are not in this table because they were
rewritten in place rather than deleted; their rewrites are named in §3.
