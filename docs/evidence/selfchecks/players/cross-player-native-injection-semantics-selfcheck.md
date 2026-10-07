# Cross-player injection semantics from the game's own data self-check

Owner cycle: `todo/mod-cross-player-native-semantics.md` Part A (cut out of
`todo/mod-content-ceiling.md` Part 2 stage 2). Decision: the injection chain stops
carrying an id/coefficient table — the game's own registries answer WHO may be
injected, the operator's client runs the item's own native limb action for the
dose, and the patient's client runs the game's own injection effect on its own
body. The host keeps the allowed operation, the resource and the arbitration.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The native limb-action dispatch | `PlayerCamera.cs:739` `ApplyWoundItem`: `usableOnLimb` → `item.Stats.useLimbAction(limb, item)`, else a `WaterContainerItem` → `ApplyToLimb(limb, 100f)` |
| 2 | The one native injection implementation | `WaterContainerItem.cs:237-261` `Inject`: `CalculateDrain` (proportional, capped) → per stack `injectionSickness` + blood viscosity → `if (liquidType.injectable) onHealthUse(ml, limb)` → `Drain` |
| 3 | The content facts | `Item.cs:7168` `Item.GlobalItems`; `LiquidItemInfo.cs:5` declares `LiquidItemInfo : ItemInfo`, and `usableOnLimb` is the PARENT's field (`ItemInfo.cs:103`); `Liquids.cs:1846` `Liquids.Registry`, `LiquidType.injectable` |
| 4 | The dose is not data | `Item.cs` per-item `useLimbAction` closures: `mult * 100f` / `150f` / `80f` (rates) and fixed `50f` / `33.334f` / `375f` / `100f`; `SyringeMinigame.cs:92` calls the delegate with `depth * Time.deltaTime` |
| 5 | The timed bodies accumulate | `CoUtils.cs:53` `DoTimedOp` adds to `remaining` for the same id, so per-delta native doses sum like the native per-frame calls |
| 6 | What was deleted | `RemoteMedicineCatalog` (15 item ids, 25 liquids, the `water` inert list), `RemoteMedicineApplication`, `RemoteMedicineLiquidEffect`, `MedicalOperationEndCommittedMsg.TimedBodyEffects`, `OperationSession.OriginalLiquids`, and the eight injection-only branches of `TimedBodyEffectApply` |
| 7 | What replaced it | Runtime `ILimbUseSemantics` / `NoLimbUseSemantics` (seam), `InjectionAdmission` (the one admission rule), `LiquidDrainPlan` (the native drain shape); adapter `GameLimbUseFacts` / `GameLimbUseSemantics` (the DI-registered seam, also used by the operator's eligibility), `RemoteInjectionUseHandler` (split out of `RemoteMedicalOperationHandler` when the architecture gate reported 680 aggregate lines for it), `NativeInjectionApply`, `Patches/RemoteInjectionPatches.cs`, `Patches/NativeLimbActionScope.cs` |
| 8 | The wire | `MedicalOperationStateMsg.AppliedDose` (field 14) and `MedicalOperationEndCommittedMsg.AppliedDose` (field 11, replacing `TimedBodyEffects`) carry the drained plan to the target |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| Host admission | `InjectionStartValidator` now asks `InjectionAdmission` (item + liquid pair from the game's data); the old "unknown liquid refuses the whole plan" allowlist is gone because the native path applies nothing for a non-injectable liquid instead of approximating it |
| Host drain | `MedicalOperationInjectionApplier` keeps `PlayerItemUseService.ApplyDrain` and the committed-ml arithmetic; the target snapshot is no longer cloned or written, because the host no longer computes the effect |
| One-shot refusal | `PlayerItemUseService` refuses an injectable container through the same predicate |
| Operator entry | `LocalUseItemEligibility` and the gesture dispatch route on the same predicate; the syringe session runs the item's own `useLimbAction` and diverts every native `Inject` call through the new patch-bridge member |
| Target apply | `MedicalOperationApply` routes the injection family to `NativeInjectionApply` and deliberately does not write the host's echoed snapshot over the effect it has not seen — neither into its own body NOR into the two display sinks (`CloneFactTable` / `RemoteMedicalCoordinator.ApplyMedicalState`), which the independent review found the first cut still fed the pre-dose values; every other family keeps that path |
| Sound | The treatment table's 15 injection rows are removed and the gate now proves the split in both directions: a table row must NOT exist for a carrier the native action decides, and every accepted id must still be decided by exactly one of the two. "Native decides" means the item's own delegate owns the clip, not that it necessarily plays one: 6 carriers play `"syringe"` in their delegate (relayed by the capture window), the 9 minigame carriers' only cue is the minigame's 2D feedback, which stays local by the existing ruling |
| Sibling chains | Topical, consume/drink, wear and limb-tool are untouched and recorded as Part B of the ticket; their tables still answer for them |
| Tests/tools/docs | The deleted catalog's 21 cases are replaced by `InjectionSemanticsTests` (9), 4 session cases and 2 tool cases are rewritten to pin the dose on the wire, the harness gained `FakeLimbUseSemantics`, and the three gates that read the deleted sources (sound census, patch-bridge census, capability catalog) were reviewed and updated |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| Drain arithmetic, no per-content constant | `InjectionSemanticsTests.DrainPlan_SplitsTheRequestedAmountInProportion`, `..._CapsAtTheContainerTotal`, `..._RefusesAnEmptyContainerOrANonPositiveDraw` |
| Admission from the game's data | `InjectionSemanticsTests.Admission_AcceptsALimbUsableContainerWithAnInjectableStack`, `..._AcceptsAMixedContainerTheOldCatalogRefused`, `..._RefusesALimbUsableContainerWithNoInjectableLiquid`, `..._RefusesAnItemTheGameDoesNotDrawFromALimb` |
| The start refusal branch (no case existed before) | `InjectionSemanticsTests.StartValidator_RefusesAnItemOutsideTheChainsOwnData`, `..._AcceptsAnInjectableContainerAndNamesItsIndex` |
| The dose reaches the target | `MedicalOperationSessionServiceTests.Host_InjectsGuest_CommitsTheDrainAndCarriesTheDoseToTheTarget`, `Guest_InjectionStartUpdateEnd_CarriesEachDoseOnceAndEmitsSingleEnd`, `Guest_InjectionCancel_FlushesBufferedDeltaBeforeCancel` |
| The host invents no effect | the same three cases assert the host's own snapshot still reads 0 opiate, and `Guest_InjectionCancel_KeepsCommittedAndSendsCancelledEnd` asserts a cancelled terminal carries no second application |
| A cancelled/empty operation keeps its guarantees | `MedicalOperationSessionServiceTests.IdleSession_TimesOutAndReleasesReservation`, `OperatorDisconnect_MidInjection_KeepsCommittedAndReleases`, `SameItem_SecondStart_IsRejectedWhileReserved`, `Guest_InjectionBurst_CoalescesIntoOneReliableFrameAfterInterval` |
| The whole drained plan travels | `MedicalToolApplicationTests.Guest_UsesCombatPenOnHost_CarriesTheWholeDoseInTheTerminal`, `Guest_UsesBloodCoagulantOnHost_CarriesTheDoseInTheTerminal` |
| The native half exists and cannot double-play | `ItemAndBodySoundCaptureGateTests.TheRemoteTreatmentTable_DecidesEveryAcceptedMedicalItem` (table-decided vs native-decided, both directions, with the handler/session/patch pins) and `RemoteMedicalTreatmentSoundCatalogTests.AnInjectableCarrier_IsDecidedByTheItemsOwnNativeActionRatherThanThisTable` |
| The seam does not widen the patch bridge | `PatchBridgePortShapeGateTests` (new member pinned in the medical port and in the aggregate's surface) |
| The limb rule the target mirrors | `InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured` (the boundary the deleted catalog's three limb-choice cases used to cover) |

## 4. Verification design and result

- Focused: `dotnet test ... --filter "FullyQualifiedName~Medical|FullyQualifiedName~InjectionSemantics|FullyQualifiedName~InteractionGate|FullyQualifiedName~ItemUse|FullyQualifiedName~SoundCapture|FullyQualifiedName~PatchBridge|FullyQualifiedName~TreatmentSound"` — green (110 behaviour + 64 gate cases).
- Full with build: behaviour **4781/4781** (net48) and gates **480/481** (net8.0), the single failure being this cycle's own delivery-checklist box while it was still unchecked; the gates read **481/481** once that box was checked (the same qualification the previous cycle recorded).
- The independent review re-ran the gate project and the focused set from the frozen tree and agreed with the focused result; it could not re-run the full suite (that was outside its brief), which is why the numbers above are THIS cycle's own final run rather than a quote.
- The native half is NOT verifiable in L0: `NativeInjectionApply`, the operator's
  native limb action, the `Inject` divert and the sound window need a game
  process. The acceptance rows for them are in the ticket and belong to a later
  agent acceptance batch; the L0 half pins the wire, the arithmetic and the
  verdict, and the gate pins that the native half still exists.

## 5. Limits recorded

- The independent review's findings all landed in this cycle's commit: the display
  sinks are no longer fed the pre-dose echo (the review's one MAJOR), the
  operator-side eligibility uses the registered `ILimbUseSemantics` seam instead of
  its own `new GameLimbUseSemantics()`, the limb rule the target mirrors gained a
  case, the sound gate states what "native decides" does and does not mean for the
  nine minigame carriers, and this file's numbers are the measured ones.
- Six older selfchecks still cite the deleted catalog by name in their own
  historical tables (`players/cross-player-medicine-use-selfcheck.md`,
  `players/cross-player-opiate-use-selfcheck.md`,
  `players/cross-player-timed-liquid-medicine-selfcheck.md`,
  `players/remote-medical-fidelity-and-syringe-minigame-selfcheck.md`,
  `players/remote-medical-treatment-operations-selfcheck.md`,
  `presentation/suppressed-native-call-sounds-stay-unheard-selfcheck.md`). They
  are records of the cycles that introduced those tables, and THIS file is the
  current evidence for the chain; their citations are left as written rather
  than rewritten.
- The dose call count on the target follows the ACCEPTED DELTA cadence, not the
  operator's frame count: `Inject`'s per-call, ml-independent
  `bloodViscosity -= injectionSickness * 0.1f` term and any per-call random roll
  follow the message cadence. For every vanilla injectable except `biochem`
  (`injectionSickness = 2f`) that term is zero.
- The target resolves an unnamed limb (`LimbIndex < 0`) itself, by the same
  most-injured rule `RemoteHealApplication` uses, on its own body.
- An injectable liquid with no `onHealthUse` delegate is logged and skipped;
  native `Inject` would throw there. A mod liquid can be in that state until
  Part 3 A of the ceiling ticket gives the mod API a delegate.
- An entirely inert container (a syringe of plain water) is refused by the
  routing predicate, which needs one `injectable` stack to keep the topical
  family (same `usableOnLimb`, its delegate calling `ApplyToLimb`) out of this
  chain; native `PlayerCamera.ApplyWoundItem` would drain it for no effect. Mixed
  containers are admitted and behave natively.
- A dose the target cannot apply (a liquid the registry does not know, or a body
  with no usable limb) is logged and skipped AFTER the host has already committed
  the ml and drained the item, so the resource is spent without an effect.
- The host's copy of a guest target advances from the target's reports, not per
  delta: a viewer's patient vitals move at the report cadence (an immediate
  re-report per applied dose, then the ordinary 1 Hz path) instead of the host
  pushing a locally computed value. The target itself now sees each dose
  immediately, which it did not before.
- The display sinks (`CloneFactTable.ApplyMedicalState`,
  `RemoteMedicalCoordinator.ApplyMedicalState`) are NOT fed this family's echoed
  snapshot, for the same reason the local body is not: it is the target's last
  report, i.e. pre-dose. The independent review found the first cut had fixed the
  body half only and left the display half writing the stale values back; the fix
  is in this cycle's commit.
- Of the 15 carriers, the 6 whose delegate plays `Sound.Play("syringe", …)`
  (antiserum, bloodbag, bloodbaghuman, bloodcoagulant, combatpen, streptokinase)
  are relayed by the capture window now that the delegate runs; the other 9 have
  no delegate-level clip at all — their only cue is the syringe minigame's own 2D
  screen feedback, which the existing ruling keeps local — so for them "native
  decides" means "the table must stay silent", which is what their deleted
  `UncarriedItems` rows recorded.
