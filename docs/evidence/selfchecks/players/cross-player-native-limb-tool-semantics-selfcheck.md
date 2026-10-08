# Cross-player limb-tool semantics from the game's own data self-check

Owner cycle: `review/mod-cross-player-native-semantics.md` Part B, the limb-tool chain — the
last table of the cross-player item-use family. Decision: applying a limb tool to another
player stops carrying a table — the item's own `ItemInfo.usableOnLimb` plus an assigned
`useLimbAction` decide the family and the treated player's own client runs that delegate —
while the host keeps the allowed operation, the resource and the arbitration.

The deleted `RemoteLimbToolCatalog` carried ten ids with a per-tool condition cost, body/limb
deltas, multiplicative factors, a required limb index, component fields and a timed ramp, every
one of them an `ldc.r4` literal inside that item's own delegate. It is the same debt the
injection, topical, drink and wear chains paid off, and it is now paid off here.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The native dispatch the family reads | `PlayerCamera.ApplyWoundItem(Item)` (`PlayerCamera.cs:739-762`): the dismembered-limb early return, the `aboveMedicalCutoff`/`ignoreDepression` alert, then `if (this.body.conscious && item.Stats.ActuallyUsableOnLimb(item))` → `if (item.Stats.usableOnLimb) item.Stats.useLimbAction(this.selectedLimb, item)`, else a `WaterContainerItem` → `ApplyToLimb(this.selectedLimb, 100f)` |
| 2 | `ActuallyUsableOnLimb` for a non-container is `usableOnLimb` | `ItemInfo.cs:10-26`: the method returns `usableOnLimb` for an item with no `WaterContainerItem`, and for one WITH a container it returns true when any stack is `healthUsable`, else `usableOnLimb` again |
| 3 | The census of the family in the shipped data | 56 items carry a `useLimbAction` delegate in `Item.SetupItems()` (census read out of `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, delegate assignments at lines 284-7086), all of them `usableOnLimb = true`. 21 of them belong to the two LIQUID chains — 19 whose delegate hands the effect to a liquid (`WaterContainerItem.Inject` or `ApplyToLimb`: the syringe family, the four topical containers, the four `Inject`-only containers) and the two blood-drawing devices (`makeshiftlrd`, `lrd`) — and 21 more belong to a session chain by name (the dressing family with `musharm`, `adhesivebandage`, `tweezers`, `wrench`, the two defibrillators and the seven amputating blades). Of the remaining 14, two (`bulbskin`, `xalorissponge`) are ALSO in the solid-food family — their own `useAction` drinks 4.5 / eats 8 (`Item.cs:2534-2544`, `:2571-2578`) — and are refused here by §2's last rule, so the chain reaches **12 ids**: the eight the deleted table carried (its ninth row, `musharm`, is the bandage session's) plus the four the table never carried (`roselight`, `glowplantfruit`, `antisepticmush`, `plasmacutter`) |
| 4 | What the deleted table transcribed | `RemoteLimbToolCatalog`: 9 ids → `RemoteLimbToolProfile` (condition cost, skin/muscle/pain/bleed deltas, two multipliers, a required limb index, blood viscosity, hemothorax, temperature, a component kind with its fields, and a timed bleed ramp). `RemoteLimbToolApplication` applied it to the host's snapshot of the target and built the component state messages; `RemoteLimbComponentKind` named the three components |
| 5 | The delegates that consume the object | `tourniquet` (`Item.cs:400-408`): `item.condition -= 0.125f`, `ComponentHolderProtocol.AddComponent<TourniquetScript>(limb).condition = item.condition`, `Object.Destroy(item.gameObject)`; `splint` (`:1479-1491`) and `carcasssplint` (`:1505-1517`): the same shape around `SplintLimb`, with `conditionLossMinute` 0.015 / 0.036 and the temporary sprite taken from the item's own `SpriteRenderer` |
| 6 | The delegates that start a timed op | `medicalsuture` (`Item.cs:376-386`): `limb.body.DoGoreSound()`, `pain += 12.5f`, `skinHealAmount += 25f`, then `CoUtils.instance.DoTimedOp("suture" + limb.name, () => limb.bleedAmount -= 4.5f, 10f)` and `item.condition -= 0.51f`. `CoUtils.DoTimedOp` (`CoUtils.cs:53`) ACCUMULATES `remaining` per id, so repeated doses on one limb sum exactly as the native calls make them |
| 7 | What was added | Runtime: `ILimbUseSemantics.IsLimbActionItem` (the seam question), `LimbToolAdmission` (the one rule), `ItemActionGrants` + `PlayerItemActionOutcomeService` (the admission and the affected-side outcome half, generalised from the solid-food eat's), `PlayerItemActionOutcomeMsg` + its handler (NetMsg 143, renamed from `PlayerItemEatOutcome`), `PlayerItemUseResultMsg.TargetRunsLimbAction` with its journal/wire twins. Adapter: `GameLimbUseFacts.IsLimbActionItem` (the game's answer), `NativeLimbToolApply` (the run on the treated player's client), `PlayerInteractionApply`'s new branch |
| 8 | What was deleted | `RemoteLimbToolCatalog.cs`, `RemoteLimbToolProfile.cs`, `RemoteLimbToolApplication.cs`, `RemoteLimbComponentKind.cs` and their 15-case test class; the whole `TimedLimbEffectMsg` chain (the message, `WirePlayerInteractionTimedLimbEffect`, the kernel `PlayerInteractionTimedLimbEffect` record, the codec's two converters, the command/event field, the result message's field, the wire field and `TimedLimbEffectApply.cs`); the treatment-sound table's six limb-tool clip rows and its two recorded silences for them |
| 9 | The sound decision | The delegates play their own clips (`"boneweld"` 696, `"splint"` 1483/1509, `"syringe"` 1613 for the chest drain, `"goo"` 1589, the suture's `DoGoreSound` 378) and run inside the medical capture scope, so the table must stay silent for them — a row would be a second decider for one clip, which is exactly what the deleted rows were. The two tools whose delegate plays nothing (`icepack`, `tourniquet`) need no recorded silence either: their native action IS the decision |
| 10 | The gesture routing and the minigame families | `RemoteMedicalOperationHandler.TryDispatchLimbUse` claims the session families first (the bandage minigame's catalog, the other-medical catalog, the shrapnel session's literal), then asks the same rule; the tail's dispatch is `DispatchedNative` for this family, which is what keeps the treatment table from replaying a clip the delegate already played |
| 11 | Wire and protocol | NetMsg 143 is RENAMED (`PlayerItemEatOutcome` → `PlayerItemActionOutcome`) and gains one member (`Consumed`); `PlayerItemUseResultMsg` gains `TargetRunsLimbAction` and LOSES `TimedEffects`; `WirePlayerInteraction` loses `TimedEffects` and gains `TargetRunsLimbAction`. `ProtocolVersion.Current` is untouched — the pre-release baseline rule (decision 241) applies and compatibility is never a design input |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| Host admission | `PlayerItemUseService`'s limb-tool branch asks `LimbToolAdmission.IsLimbTool(_limbUseSemantics, …)` exactly where the deleted catalog was asked; the branch commits NOTHING and publishes the request half |
| Host outcome | `PlayerItemActionOutcomeService.HandleOutcome` takes the grant, finds the item on its OWNER, asks the item's own data which family the report belongs to (the solid-food verdict first, then the limb-tool rule), and commits `msg.Condition` + `msg.Consumed` through the shared `PlayerItemUseCommit` |
| Operator entry — native WoundView limb gesture | the tail asks the rule and returns `DispatchedNative`, so no table clip is replayed; the request carries the selected limb |
| Operator entry — world drag | NOT an entry for this family: a world drag is the cross-player form of USING an item from the inventory, so `LocalUseItemEligibility.IsUseItem` asks only the use-action families and the wear rule, and a limb tool — which has no `useAction` at all — falls through to the native drop |
| Operator entry — held remote item | unchanged: the same shared `TryExecuteUse` reaches the same branch, and it carries the requester's own limb selection |
| The host's limb requirement | `PlayerItemUseService` refuses a limb-tool request whose `limbIndex < 0` — the affected side's most-injured limb is never used as a guess — which is what keeps the two entries from being crossed even by a hand-made request |
| Affected side | `PlayerInteractionApply` runs `NativeLimbToolApply` outside the `RemoteApply` scope, on the local body's own limb and the standing object of the offered item |
| Auto-select and usability | `CarriedItemUseTree.IsActuallyUsable` / `FindFirstUsable` ask the rule, so the host's auto-select cannot offer an item the chain refuses |
| Sound | the treatment table's rows for this family are gone, the gate's `VanillaLimbToolItems` census makes a re-added row fail, and `RemoteMedicalTreatmentSoundCatalogTests` pins both directions |
| Sibling chains | the solid-food eat keeps its semantics through the shared outcome half (its report still sends `consumed: false` and the host keeps its own verdict); injection, topical, drink and wear are untouched; the heal/bandage/shrapnel/other-medical sessions keep their claims by name, and the solid-food family's claim is asked through the item's own data (an item that both feeds a body and carries a limb action stays on the eat family, because the request cannot say which gesture was meant) |
| Data/state | no new mutable state beyond the shared grant table; the family adds one seam question and one rule |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| The request half changes nothing: the host admits, computes no body state and spends no item | `MedicalToolApplicationTests.Guest_UsesBoneweldingToolOnHost_AdmitsItAndChangesNothing` (blood viscosity and bone-heal timer untouched, item condition still 0.75), `…Guest_UsesSplintOnHost_IsAdmittedWithoutSpendingTheItem` (no component, no limb latch, item still carried), `…Guest_UsesMedicalSutureOnHost_AdmitsItWithoutAHostTimedEffect` |
| The outcome half commits the item's own state onto its owner | `…AffectedSideOutcome_CommitsTheConditionTheOwnActionLeft` (condition 0.25 on the owner's snapshot, the transfer table and the second result) |
| A consumed tool removes the owner's row | `…AffectedSideOutcome_ConsumedComponentTool_RemovesTheOwnersRow` (item destroyed, both kernels' rows leave the carried location) |
| One admission, one report | `…AffectedSideOutcome_WithoutAnAdmittedUse_IsRefused`, `…AffectedSideOutcome_Twice_SettlesTheItemOnce` |
| The rule admits the game's data and refuses every unmigrated claim | `LimbToolAdmissionTests` (5 cases: a deleted-table id, two ids the table never carried, the two body-feeding ids that stay on the eat family, one id per claimed chain, and the seam-first refusals) |
| The tweezers refusals the chain inherited stand unchanged | `MedicalToolApplicationTests.DirectUseRequest_WithTweezers_IsRefused_AndShrapnelStays`, `…Tweezers_NoShrapnelOnTarget_IsRefused` |
| The family's ONE entry is the medical view: a request that names no limb changes nothing | `MedicalToolApplicationTests.Use_ALimbToolWithNoLimbNamed_IsRefused` (no result, no limb latch, no component, the item still carried), pinned as a source shape too: the world-drag gate's tree names no `LimbToolAdmission`, and the service carries the `limbIndex < 0` refusal |
| The treatment table must stay silent for this family and keep its session rows | `RemoteMedicalTreatmentSoundCatalogTests.ALimbTool_LeavesItsClipToItsOwnNativeAction` (8 ids), `…ASessionFamilyTool_CarriesTheClipItsBlockedNativeCallWouldHavePlayed` (3 rows) |
| No id-keyed table may grow back in the chain's sources, the adapter's answer stays the game's registry, the run stays on the affected side, and every claim is named | `LimbToolChainGateTests` (5 cases: the table census, the game-data answer, the run's shape, the rule's claims, the matcher's 6 samples) |
| The kernel/wire round trip carries the new flag and no longer carries the timed effects | `PlayerDomainKernelTests`' item-use round trip (asserts `TargetRunsLimbAction` true and `TargetEatsTheItem` false through `KernelWireMapper`) |
| The renamed report is still declared in the wire vocabulary | `SyncCoverageGateTests` (matrix + evidence quote re-pointed), `GuestToHostDirectionTests` (the direction list) |

## 4. Verification design and result

- Build: `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- Focused: `dotnet test … --filter "FullyQualifiedName~MedicalToolApplicationTests|FullyQualifiedName~ItemUseTests|FullyQualifiedName~PlayerDomainKernelTests|FullyQualifiedName~GuestToHostDirectionTests"` — 89 cases, green after the two expectation fixes this cycle's own first run exposed (the suite's `Item()` helper carries condition 0.75, not 1.0; the assertions now state that rather than the chain's old cost).
- Gates: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName!~DeliveryChecklist"` — **557/557**, green. The reds the first gate run reported were all this change's own follow-ups and each is fixed at the source: the renamed `NetMsg` member in the matrix vocabulary and its evidence quote, the en/zh pair re-record in `docs/standard/alignment.txt`, the treatment table's two anti-emptying floors (9 clip rows → 3, and the decided census 30 → 22, both exactly the migrated rows) plus the new `VanillaLimbToolItems` census, one `System.Array` that had to become an `Array` (the fully-qualified-name gate), and the backlog index row the moved ticket pushed one character over its 160-byte budget. The independent review then found the two blockers §2's last rule and `NativeLimbToolApply`'s deferred report close, and both fixes are in this same commit.
- Full WITH build: `dotnet test CasualtiesUnknownOnline.slnx` — behaviour **4798/4798** (net48, 47 s) and gates **558/558** (net8.0). The cycle's own intermediate runs are the reason the numbers read differently in places: 4796/4797 before the review's last case and the user-found entry fix, and 557/558 with the only red being `DeliveryChecklist_NoIncompleteRequiredBoxes` while the checklist above was still being filled — the gate working rather than failing.
- Coverage direction, stated rather than implied: every L0 case here is the guest-operator → host-target direction, which is the direction the deleted chain's cases had. The host-operator → guest-target direction shares the same host entry and the same affected-side apply but no case drives it, and no case drives a third peer — both are acceptance-batch rows.
- The native half is NOT verifiable in L0: `Item.GlobalItems`, `ItemInfo.useLimbAction`, `StandingItemMaterializer` and `Body.limbs` need a game process, so `GameLimbUseFacts`, `NativeLimbToolApply` and the delegate's own effects are pinned by the gate's source walk and by the acceptance batch, not by an L0 case. What L0 pins is the admission, the two-step request/outcome shape, the commit, the grant rule and the wire.

## 5. Limits recorded

- **The family has exactly ONE entry, and the user settled it while reviewing this cycle.** A limb tool
  has no `useAction` at all — only a `useLimbAction` — so the inventory-use gesture (a world drag onto a
  player) cannot mean it, and `IsUseItem` no longer admits the family; the medical view carries the limb
  the operator picked, and the host refuses a request that names none. This cycle's first draft admitted
  the family from the world drag as well and resolved the missing limb to the affected side's
  most-injured limb, which is a guess at the one thing the action is about — and the reach it inherited
  from the deleted catalog had the same defect. The rule the review settled: the inventory-use gesture
  carries `useAction`, the medical view carries `useLimbAction`, and an item that has both is two
  different actions in two isolated flows that nothing may arbitrate between by family order.

- **Mod content still cannot author a limb action** (`ModItemDefinition` has no limb-use behaviour — Part 3 A of the ceiling ticket), so this chain's reach grows over VANILLA items the deleted table missed, not over mod-authored ones. The predicate asks no id, so it is ready the day the DTO can declare one.
- **The gesture families keep their claims.** An item whose native action is a minigame (the dressing family, `musharm`, `tweezers`, `wrench`, the defibrillators, the seven amputating blades) stays with the session chain that owns its gesture: natively that minigame is played by the body's owner, so routing it here would hand the treated player the operator's work. `LimbToolAdmission` names every claim, one line per chain, and the ticket that migrates one removes its own line.
- **One user-visible change follows from that boundary.** A `musharm` dragged onto a teammate in the WORLD was applied by the deleted table's one-shot numbers (condition cost 1, skin heal 8, bandage slow 10) and is now refused exactly like every other dressing, because the bandage minigame's claim covers the wound-view path and the world drag is the one-shot path. Native `musharm` is a 2.5-turn minigame, so the old behaviour was the anomaly.
- **A consumed tool's condition is not carried.** The component-bearing tools destroy the object they are handed, so the affected side reports condition 0 with `Consumed`; the item's condition at that moment lives on the limb component the native delegate filled (`TourniquetScript.condition = item.condition`), which is where the game keeps it. The host removes the row from `Consumed`, never from the number.
- **A destroyed standing object is re-materialized if its row is still in the data.** The native delegate destroys the object before the host's commit lands (the report waits one frame for that destroy, and the commit needs a round trip), and the materializer's reconcile treats a destroyed incarnation as absent and rebuilds it for a row that is still wanted. The rebuilt object is inert (the recipe's switches) and `RetireUnwanted` takes it when the host's commit removes the row; recorded because a reader of the logs would otherwise see a materialize line after a consume.
- **Only the item's condition travels back.** The report carries condition + consumption and nothing else, so a delegate that writes another part of the ITEM (a component's state, a battery a `plasmacutter` drains — `Item.cs:4627`'s own `useAction` family) leaves the owner's copy at its old value: the same shape the solid-food chain's report has, named here because this chain's newly admitted ids include one. What the delegate writes to the LIMB is unaffected — that is the patient's own state and reaches the host through its ordinary report.
- **A locally refused application leaves its grant in place.** When the affected side cannot run the action (no standing object, no usable limb, the item left the inventory) it logs and reports nothing, so the host's admission stays until the session ends. Bounded by construction (one entry per admitted use) and harmless (a report needs the grant, and the grant alone writes nothing), but a session that sees many refused uses accumulates entries.
- **The affected side asserts the owner's item state.** The reported condition is written onto the owner's item without a host-side cap, because the host has no second source for what a delegate spent; `Consumed` can likewise destroy the row. This is the same trust boundary the two liquid chains record for their dose's VALUE, and it takes a modified client to reach.
- **No-op delegates are reported as runs.** The applier cannot tell a delegate that moved the limb from one whose own guards refused (a `chestdrain` on the wrong limb, a component tool on an occupied one): the log says the game's own action ran, which is literally true, and the old chain's by-name refusals for those cases are gone with the table — the game's own guard is the refusal now.
- **The native gate on the treatING body is not reproduced.** Native asks `body.conscious` and the depression cutoff of the player who ACTS; in a single-player game that is also the player treated, so the pair is not a "may this limb be treated" rule cross-player. The operator's own gesture gates stay the operator's. (The HOST still refuses a request whose TARGET is not conscious/alive — `TryExecuteUse`'s own gate — which is a different question, and the ticket's note that the treated side is often unconscious by design is about the DELEGATE's gate, not the host's.)
- **The run happens against the standing object's state**, i.e. what the last authoritative fact left (condition, liquids, components). A tool whose delegate reads a field the fact path does not carry would read the prefab's default; true for the nine deleted ids (their delegates read `item.condition` and their own sprite) and named here as the general limit.
- **The native half needs a game process.** Whether the run lands on the right limb, whether the component appears on the treated player's body, whether the delegate's clip is relayed from it, whether the one-frame deferral sees the destroy it was written for, and what a third peer sees are acceptance rows.

## 6. Disposition of what the deleted table was pinned by

`RemoteLimbToolApplicationTests` (15 cases) tested the deleted classes directly and is gone with
them; what it asserted about the EFFECT is now the game's own code on the affected side, which no
L0 case can reach. The chain's two steps are covered instead: the request half (three cases,
including the "changes nothing" assertions the old cases made impossible to separate from the
effect) and the outcome half (three cases, including the consumed-tool shape the old table decided
by its own `DestroyAtZero` flag rather than by the delegate). Three of the old assertions have no
successor by construction and are named so nobody looks for them: the tourniquet's
`BlockedBleeding`/`TourniquetScript` state, the splint's `conditionLossMinute` and `item` fields, and
the icepack's `ChilledLimb` timing — all three were CUO-written component state, and the component
is the native `AddComponent` call's now. TWO mutations were applied to the frozen candidate and
reverted, each run against a rebuilt tree, so the new cases' discrimination is measured rather than
claimed: (a) the harness seam's answer inverted (`FakeLimbUseSemantics.IsLimbActionItem` →
`!Contains(itemId)`) → 9 of the 20 cases in `MedicalToolApplicationTests` + `LimbToolAdmissionTests`
redden (the three admission cases that expect an admitted id, all three request-half cases, all three
outcome cases), while the refusal cases stay green because a refused gesture has no result to read;
(b) the reported consumption ignored in `PlayerItemActionOutcomeService` (`destroyed = msg.Consumed` →
`false`) → exactly 1 case reddens, `AffectedSideOutcome_ConsumedComponentTool_RemovesTheOwnersRow`,
which is the only case that can discriminate it. What no L0 mutation can show is the affected-side
half: removing `PlayerInteractionApply`'s `TargetRunsLimbAction` branch leaves the whole suite green,
because the game-side run needs a game process — that is what makes it an acceptance row.

What the table pinned that nothing pins any more is its own nine-row coverage — that coverage is now
the game's data, and the gate's `VanillaLimbToolItems` census plus §1 row 3 name the ids a reader
would otherwise have to reconstruct.

One prose reference is left stale on purpose rather than half-updated: the rows of
`review/remote-medical-treatment-operations.md` that describe the three deleted catalogs
(`RemoteMedicineCatalog`, `RemoteTopicalCatalog`, `RemoteLimbToolCatalog`) as the live routing
surface. That ticket is a record of the cycle that introduced them, the two earlier deletions
already left their rows standing, and the acceptance batch reads the code rather than that list.
