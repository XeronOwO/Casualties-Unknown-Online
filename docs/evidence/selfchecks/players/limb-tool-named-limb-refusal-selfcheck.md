# The limb-tool family's named-limb refusal self-check

Owner cycle: `review/limb-tool-family-protection-gaps.md` — the independent review of the entry-mapping
record cycle found the family's own protections weaker than the record said (its findings M1 and m2).
Decision 246 is the rule; this cycle makes the code obey it on the affected side and widens the pin that
guards the other entry.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The affected side's resolve, before this cycle | `NativeLimbTarget.Resolve`: the guard on the named index, then a `skinHealth + muscleHealth` minimum over the attachable limbs — and the caller `NativeLimbToolApply.Apply` took that answer, so a limb this body could not serve became the most injured one |
| 2 | The request path's only limb check | `PlayerItemUseService`: `if (limbIndex < 0)` refuses the family ("is a limb tool but the request named no limb — it is applied through the medical view, which carries the limb the operator picked"), then publishes `LimbIndex = limbIndex` unchanged — an index past the patient's own layout is forwarded |
| 3 | The siblings that keep the automatic rule | `NativeInjectionApply` and `NativeTopicalApply` (both `NativeLimbTarget.Resolve`), `CrossPlayerDragUse.ResolveMeasureLimb` (a topical MEASUREMENT on the remote clone), and the host's heal path (`PlayerHealService` → `RemoteHealApplication.ResolveLimbIndex`). Their `-1` limb is a legal auto-select, and their cases pin the fallback in the Runtime twin the adapter mirrors: `InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured` and `RemoteHealApplicationTests.ResolveLimbIndex_FallsBackToMostInjuredForInvalidOrDismemberedRequest` both run `RemoteHealApplication.ResolveLimbIndex` (its own comment records that a live `Body` is not L0-reachable) |
| 4 | What was added | `NativeLimbTarget.ResolveNamed` (the named limb, or nothing) with `Resolve` rebuilt on it; `NativeLimbToolApply` asks the named question and refuses by name; two new facts in `LimbToolChainGateTests` (the scoping contract case and the isolation scan), with the resolve assertions tightened inside the existing run fact — the class goes from 5 facts / 11 cases to 6 facts / 12 cases |
| 5 | What the new gate facts read | the apply's resolve name (positive and negative) in `NativeLimbToolApply.cs`, and the isolation scoped to `LocalUseItemEligibility`'s `IsUseItem`/`FamilyOf` (no limb rule) versus `IsMedicalLimbUseItem` (asks it) |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| The limb-tool apply | asks the NAMED question now: a limb this body cannot serve is refused with a log naming the item and the index, nothing is reported, and the admission grant stays — exactly the family's existing local-refusal path (no standing object, the row left the inventory) |
| The injection and topical applies | unchanged, and they must stay: their `-1` is a legal auto-select and their own cases pin the fallback |
| The world drag's topical measurement | unchanged: `CrossPlayerDragUse.ResolveMeasureLimb` measures on the remote clone's most-injured limb. It is the same "the gesture carries no limb" pattern for a DIFFERENT family, and it is an OPEN question this cycle deliberately does not answer — whether that family's world drag should exist at all, or move to the medical view where the operator picks the limb, is not settled by decision 246 (the family ticket records the topical family's own world-drag limit), and nothing changes for it here |
| The heal family's host-side resolve | untouched (`PlayerHealService` → `RemoteHealApplication.ResolveLimbIndex`) |
| The two entries' isolation | the world drag's gate (`IsUseItem`/`FamilyOf`, also asked by the bridge's two dose-measurement queries) names no limb rule, and the medical gate (`IsMedicalLimbUseItem`) is where the limb rule IS asked — now pinned in both files, where the pin used to read `CrossPlayerDragUse.cs` alone |
| Wire, protocol, content | no message, no field and no protocol number changes (decision 241's freeze stands); no item id is named anywhere in the change |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| The limb the request named is the limb that is served, and the fallback is gone | `LimbToolChainGateTests.TheRun_LandsOnTheAffectedSidesOwnLimbThroughItsOwnObjectOfTheItem` — asserts `NativeLimbTarget.ResolveNamed` is asked and `NativeLimbTarget.Resolve` is NOT (this fact is the cycle's red: it fails on the pre-fix tree) |
| A half-fix cannot pass: asking the strict resolve and then falling back anyway | mutation A (reverted): the resolve line became `NativeLimbTarget.ResolveNamed(...) ?? NativeLimbTarget.Resolve(body, -1)` → the fact's negative assertion reddens (`Assert.DoesNotContain() Failure: Filter matched in collection`), while the positive one still passes |
| The world drag cannot claim the family again, in ANY spelling | mutation B (reverted): `LocalUseItemEligibility.IsUseItem` gained `if (LimbToolAdmission.IsLimbTool(...)) return true;` → `TheWorldDragGate_AsksNoLimbRuleAndTheMedicalGateIsWhereItIsAsked` reddens. The OLD pin stayed green under this mutation, because it read `CrossPlayerDragUse.cs` only — that is the loophole this fact closes |
| ...nor by hiding the rule's name behind a directive | mutation C (reverted): the file gained `using Limbs = …LimbToolAdmission;` and `IsUseItem` asked `Limbs.IsLimbTool(...)` → the same fact reddens, because the scan RESOLVES the file's own alias (and a `using static`'s bare members) instead of matching one literal name — the shape decision 245's content-kind gate uses |
| The isolation scan is scoped to the gate members, not to the whole file | `TheMemberScoping_ReadsTheNamedMemberAndNotItsNeighbour` — a synthetic source where the asking method is seen, its neighbour is not, and an undeclared name yields nothing; both real scans also carry a "found something" floor |
| The sibling chains keep the automatic rule | no source change to them, and their cases stay green in the behaviour suite below |

## 4. Verification design and result

- Build: `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- **Red observed before the fix**: the new apply fact failed against the pre-fix tree
  (`Assert.Contains() Failure: Filter not matched in collection`, the collection listing the apply's own
  member accesses) — that tree asks `NativeLimbTarget.Resolve`.
- Focused: `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName~LimbToolChainGateTests"`
  — 12/12 green after the fix, and 11/12 on the first (red) run, whose single failure is the new fact.
- Full: behaviour **4798/4798** (net48, 52 s) and gates **559/560** (net8.0) mid-cycle, the only red being
  `DeliveryChecklist_NoIncompleteRequiredBoxes` while the checklist was still being filled — the gate
  working rather than failing. The final gate run, after every box was filled and the review's findings
  were fixed, is **560/560** (net8.0), and the checklist's own box 7 carries that reading.
- The native half is deliberately not L0-reachable: `Body.limbs` needs a game process, so what this tree
  pins is the SOURCE SHAPE (which resolve the apply asks) and what the acceptance batch must read is
  whether the refusal happens and its log names the item and the index.

## 5. Limits recorded

- **The refusal is silent to the players**, like the family's other local refusals: one warning on the
  affected side, the host's session-end line naming any admission left unsettled
  (`PlayerItemActionOutcomeService`), nothing reported, and the admission grant stays until then. Bounded
  by construction (one entry per admitted use, and the grant alone writes nothing), but a session that
  sees many refused uses accumulates entries.
- **The host still validates only `limbIndex < 0`.** It cannot answer the layout question — it holds no
  live limb layout of the patient's body — which is why the refusal lives on the affected side
  (decision 184: the judgment about a player's body belongs to that player's client).
- **The topical world drag keeps its most-injured-limb resolve** (see §2). Whether that family's world
  drag should exist at all, or move to the medical view where the operator picks the limb, is an OPEN
  question — it is not settled by the limb-tool ruling, and this cycle does not decide it.
- **How often the pick can go stale mid-flight** (a limb dismembered between the operator's pick and the
  run) is an acceptance reading, not something this tree can measure; the refusal path is what this cycle
  makes correct either way.
