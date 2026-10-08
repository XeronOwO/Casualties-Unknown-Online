# The topical entry and its named limb self-check

Owner cycle: the topical chain of `review/mod-cross-player-native-semantics.md`. A topical application is the
**medical view's** action — its native call site is the same wound-view release the limb tool uses — so the
world drag (the inventory use: eat, drink, wear) must not carry it, and the limb it lands on is the one the
operator picked (decision 246, the user's 2026-10-08 ruling applied to this family).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The world drag's admission, before this cycle | `LocalUseItemEligibility.IsUseItem` admitted every family `FamilyOf` answered — the TOPICAL one included — and `CrossPlayerDragUse` measured a topical dose on the remote clone's most-injured limb (`ResolveMeasureLimb` → `NativeLimbTarget.Resolve(body, -1)`) before sending a request whose `limbIndex` stayed `-1` |
| 2 | The host's topical branch, before this cycle | `PlayerItemUseService`'s `TopicalAdmission` branch built the drain plan and published `LimbIndex = limbIndex`; with `-1` (a world drag) it never asked which limb the dose was for |
| 3 | The affected side, before this cycle | `NativeTopicalApply.Apply` resolved the index through `NativeLimbTarget.Resolve`, i.e. the most injured attached limb whenever the named one could not be served |
| 4 | The medical entry, already correct | `RemoteMedicalOperationHandler.TryStartRemoteTopicalUse` validates the display limb and sends `SendUseRequest(target, itemInstanceId, limbIndex, doseMl)` — the limb rides the request |
| 5 | What was added | `LocalUseItemEligibility.IsWorldDragFamily` (with `IsUseItem` asking it), `TopicalUseBranch.TryPlan` (the host's half, split out at the 600-line gate), `NativeTopicalApply` asking `NativeLimbTarget.ResolveNamed`, and `TopicalChainGateTests` (2 facts) |
| 6 | What was deleted | the world drag's topical measurement case and `ResolveMeasureLimb` — unreachable once the family is not admitted, and the last place that chose a patient's limb on the operator's client |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| The world drag | now carries wear and the item's own use-action families (drink, solid food) plus the injection family, which is admitted only so the host can refuse it by name (its native use action draws blood). A topical carrier is not admitted, so the release falls through to the **native drop** — which is exactly what the native inventory use means for it, since every topical carrier is `usable=false` and the native world release has no limb to offer |
| The medical view | unchanged and now the only entry: it validates the limb on the display body and puts it on the request |
| The host | refuses a topical request that names no limb (a hand-made or stale request commits nothing); the drink, feed and wear families are untouched, because they never needed a limb |
| The affected side | applies to the limb the request named, or refuses with a log naming the item's limb; the INJECTION chain deliberately keeps the automatic rule, because a `-1` limb is a legal auto-select for it (`MedicalTargetBodyValidator` does not scope it) |
| The limb tool | the sibling family, isolated the same way one cycle earlier (decision 246's first half) |
| Wire, protocol, content | no message, field or protocol number changes (decision 241's freeze stands); no item id is named in the product change (`TopicalUseBranch`, `NativeTopicalApply`, `LocalUseItemEligibility` and both gate facts carry none — the test file necessarily names the fixtures it builds) |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| The world drag does not carry the topical family | `TopicalChainGateTests.TheWorldDragGate_DoesNotCarryTheTopicalFamily` — red on the pre-fix tree (`IsWorldDragFamily` does not exist, and the census names `Topical` once it does); the mutation that re-adds `or Family.Topical` was measured and reverted |
| The affected side serves only the limb the request named | `TopicalChainGateTests.TheTopicalApply_ServesOnlyTheLimbTheRequestNamed` — red on the pre-fix tree (`NativeTopicalApply` asked `NativeLimbTarget.Resolve`) |
| The host refuses a topical request that names no limb | `ItemUseTests.Use_ATopicalRequestThatNamesNoLimb_IsRefused` — red on the pre-fix tree (the request was accepted and a result event published), green after |
| The medical entry still carries the operator's limb | `ItemUseTests.Guest_UsesTopicalOnSelectedLimb_CarriesTheSelectionToThePatient` — unchanged and green |
| The commit half is unchanged (the dose still rides the request) | `ItemUseTests.Guest_UsesPaincreamOnHost_CommitsTheDoseAndCarriesItToThePatient` — now sends the limb the medical view sends; its assertions are untouched |
| The tests whose request shape had to name a limb so they still exercise their OWN branch | `Use_ATopicalRequestWithNoMeasuredDose_IsRefused` (the topical carrier with no dose — without a limb the new wound-view rule refuses it first) and `Use_AnItemNoChainClaims_IsRefused` now name a limb, so each is refused by the branch its name claims (the dose plan, the chain's final else) rather than by the new rule; `Use_AContainerHoldingBothKinds_IsRefusedByTheInjectionFirstOrder` is refused by the injection rule whichever limb it names and keeps its limb for the same reason |

## 4. Verification design and result

- Build: `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- **Reds observed before the fix**: the behaviour case (`Assert.DoesNotContain() Failure: Filter matched in
  collection` — the result event existed) and both gate facts (`Assert.Contains() Failure` on the family
  census and on `NativeLimbTarget.ResolveNamed`).
- One structural consequence was found by the gates rather than by review and is fixed in the same round:
  `PlayerItemUseService` crossed the 600-line architecture gate (601 lines by the gate's own count, an
  in-session reading — the committed tree holds the extracted state), so the topical half moved into
  `TopicalUseBranch` (the file is back to 580).
- Full: behaviour **4799/4799** (net48) and the gate project green with every checklist box filled; the
  focused pair of chain gates is 14/14.
- The native half is deliberately not L0-reachable: whether a live session's dose lands on the picked limb is
  the acceptance batch's row.

## 5. Limits recorded

- **A topical carrier released on a teammate in the world now drops.** That is the native inventory use for
  a carrier the game cannot use from the backpack (`usable=false`), and it is the same shape the limb tool
  already had; the operator applies it through the wound view instead.
- **The host's refusal is reachable through a validated production intent, not only a hand-made one**:
  the remote-item release names no limb when the wound view is not readable
  (`RemoteDragMutationPatches`, `limbIndex = woundView != null ? woundView.limbLookingAt : -1`) and the
  intent layer blesses `-1` as legal, so this refusal is the place a degraded read ends — refused, with the
  item untouched, rather than applied to a limb nobody picked.
- **A container whose LIVE stacks make it topical while its own data says it is usable is refused by the
  world drag, and that is a recorded gap rather than a solved case.** The game's own liquid-transfer
  gesture lets a player put a health-usable liquid (`disinfectant`, `alcohol`, `reliefcream`, …) into a
  `saline` bag, whose item data is `usable = true` — so its own inventory use is a DRINK. `FamilyOf` asks
  the topical rule first and reads the live stacks, so that container is classified topical and dropped by
  the world drag. Nothing got worse (before this cycle it took a topical dose on the most-injured limb),
  but the isolation rule would classify it by the item's own data (a drink) instead of refusing it; parked
  on `todo/topical-live-stack-family-order.md`.
- **How often an operator's pick goes stale mid-flight** (a limb dismembered between the pick and the run)
  is an acceptance reading; the refusal path is what this cycle makes correct either way.
