# A dead or unconscious carried body keeps its vitals — self-check (2026-09-26)

Ticket: `docs/backlog/review/carried-unconscious-body-simulation.md`. Cycle scope: the pinned-ragdoll
half of the carried-body rule — a dead or unconscious carried body's own client keeps the VITALS
stages of the game's per-frame pass while the carry relation keeps owning its pose, its physics, its
ground contact and its sounds. This extends decision 216
(`review/carried-rider-own-body-stops-simulating.md`), which covers the conscious/alive rider.

## 1. Mechanism inventory

| # | Mechanism | Evidence (game) | Change | Evidence (ours) |
|---|---|---|---|---|
| 1 | `Body.Update` is the only caller of every per-frame body stage, and a pinned carried body skipped the whole pass, so nothing advanced its vitals | `Body.cs:2574-2591` (the ten calls), `Body.cs:818`/`Body.cs:888` (heart rate and the ECG driver inside `HandleBody`) | the adapter runs the vital-sign stages for that body under a new rule gate | `CarriedBodyVitalsSubsetPinTests` (stage set, order, counts), `CarriedBodySimulationTests.RunsVitalsSubset` rows |
| 2 | The circulation stage reads `tempDiffFromNormal` (`num3 += this.tempDiffFromNormal * 3f`, `Body.cs:712`), whose ONLY writer is `HandleVariableUpdates` (`Body.cs:3367`) | `Body.cs:712`, `Body.cs:765`, `Body.cs:794`, `Body.cs:859` (readers), `Body.cs:3367` (writer) | `HandleVariableUpdates` is part of the subset — a body whose counters froze would feed the circulation stage a stale temperature delta | the pin's stage list; the helper's doc comment names the reason |
| 3 | `HandleBody` can ragdoll the body (pain over 99, `Body.cs:2776-2780`) and `Ragdoll()` re-enables the limb rigidbodies (`limb.rb.simulated = true`, `Body.cs:1723`), which must never simulate under a root the placement teleports every frame | `Body.cs:2772-2780`, `Body.cs:1713-1730` | the subset runs with the rigidbodies frozen before AND after it | the pin's block shape (freeze → subset → freeze) plus its two one-freeze counter-examples |
| 4 | The stages that own what the carry relation owns are separable: the 60 s ragdoll stand timer and pose (`HandlePhysics`, `Body.cs:3080-3083`), the ground pass (`HandleGroundedState`, `Body.cs:2594`), the animator limb copy (`HandleVisuals`, `Body.cs:3123`), the fall scream reading the carrier's velocity (`HandleSounds`, `Body.cs:2751`) and the conscious-only water-shake animation (`HandleDogWaterShaking`, `Body.cs:3326`) | the five sites above | all five stay OUT of the subset | `HandleVisuals` is rejected by the subset METHOD's exact-body equality (its name legitimately appears in the proxy path, so a bare-name scan cannot cover it); the other four are rejected by a file-wide name scan, and a counter-example pulls `HandlePhysics` into the subset |
| 5 | `Limb.Update` is the limb wound/infection pass — it drains blood volume, writes the limb shader params and consumes `Random` — and it carries NO alive guard, and nothing disables a limb component on death | `Limb.cs:498-603` (`Limb.cs:500`, `Limb.cs:501-506`, `Limb.cs:535`), `Limb.cs:404` is the dismembered-sprite change, not a disable | the limb gate becomes "every LOCAL body's limbs run, only a clone's do not" | `CarriedBodySimulationTests.EveryLocalBody_RunsItsLimbsAndOnlyACloneSkips`; the 16-row matrix asserts the limb predicate agrees with the mode; the pin holds the gate and rejects HEAD's shape |
| 6 | The limb gate used to read the parent body's carry state through `CarriedBodyDriver.IsCarryingInParent`; the new rule reads only "is this a remote clone" | — | the helper is deleted (its last caller was this gate) | grep: `IsCarryingInParent` has no caller left in `src/` |
| 7 | The carry trace's `mode=` word only knew two states, so a real session could not tell the vitals-only treatment from a frozen body | `CarrySimulationTrace` | the word now comes from the rule's three-way mode (`native-simulation` / `vitals-only` / `pinned-ragdoll`) | `PlayerInteractionApply.SimulationMode`; the trace is one line per relation change and one per second at Debug |

## 2. The decision, in one place

`CarriedBodySimulation.Treatment(isRemoteClone, isLocalCarriedBody, alive, conscious)` is the single
owner of the three-way answer — `Proxy` (a remote clone: presentation only), `VitalsOnly` (a dead or
unconscious local carried body: the vital-sign stages under a pinned pose) and `Full` (every other
local body). `KeepsNativeSimulation` is the carried-rider predicate `Treatment` is built on;
`SkipsNativeSimulation`, `RunsVitalsSubset`, `SuppressesMovement` and `CarrierOwnsGroundContact` are
its views of that one answer (the last two are the carried-rider view, which is the only population a
carry flag can be true for), and `RunsLimbSimulation` is the family's limb half, decided by the proxy
flag alone because a limb pass carries no pose — the rule matrix asserts the agreement of all of them
with the mode over all 16 flag combinations, and a census asserts the three mode names (a fourth mode
is a decision, not an implementation detail). The rule matrix agrees with the reviewer's independent
reflection of the compiled `Treatment` over the same 16 inputs.

## 3. Whole-family audit

| Family member | Verdict | Reason |
|---|---|---|
| A remote clone (`Mode.Proxy`) | UNCHANGED | It is not simulated on this client at all; the clone's vitals are its owner's. |
| A conscious/alive carried rider (`Mode.Full`) | UNCHANGED | Decision 216's rule: the whole native pass runs and only the transform/input belong to the carry. The subset must NOT run on top of it, or the same stages would advance twice in a frame. |
| A dead or unconscious carried body (`Mode.VitalsOnly`) | NEW | This cycle's change: vitals advance, pose/physics/ground/sounds stay with the carry relation. |
| An ordinary local body | UNCHANGED | `Mode.Full`, and it never takes the subset path. |
| `Body.FixedUpdate` for the pinned body | UNCHANGED (still skipped) | The carry relation owns the body's physics; the game's integrator under a teleported root is the twitch family the rule exists to remove. The pin holds the gate in place. |
| `Limb.Update` for the pinned body | CHANGED (now runs) | Item 5: the pass is wound/infection state, is not pose, and the game runs it for a corpse. |
| `HandleSounds` for the pinned body | STAYS OUT (explicit decision) | Its one stage is the fall scream, which reads the rigidbody velocity — the carrier's own — and would report the carrier's fall as the carried body's. |
| `HandleVariableUpdates` / `HandleDogWaterShaking` | IN (the counter stage) / OUT (the shaker) | The counters are a vitals input (item 2); the shaker is a conscious-only animation trigger, so it can only fire for a population that is by definition not this one. |
| The movement gate (`SuppressesMovement`) | UNCHANGED | Still tied to `Mode.Full`: a pinned body's input is not gated by this rule because it does not run the native pass at all. |
| The carry trace | EXTENDED | Item 7: the third mode must be nameable in a real session's log. |

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| The rule's decision | one three-way owner with derived views | `CarriedBodySimulationTests` (16-combination matrix + three-mode census; each derived predicate asserted against the mode) |
| The vitals stage set | exactly five stages of `Body.Update`, in a method of their own | `CarriedBodyVitalsSubsetPinTests`: the subset method's exact body, the call site's block shape and its position on the skipped branch; dropped / duplicated / re-ordered / wrong-API / hoisted-out-of-the-method counter-examples |
| The pinned pose and physics | frozen on both sides of the subset | the pin's block shape + its two one-freeze counter-examples |
| The excluded stages | never in the subset, and the four that have no other role never in the file | the subset method's exact-body equality (covers `HandleVisuals`), the file-wide name scan for the other four, and the counter-examples that pull `HandlePhysics` and `HandleVisuals` in |
| The limb pass | local bodies run it, clones do not | the rule matrix row and its mode-agreement assertion + the pin's limb gate and its pre-fix counter-example |
| The physics step | still skipped for the pinned body | the pin's `BodyFixedUpdatePatch` gate + its counter-example |
| The skipped branch | the subset call site never moves into the whole-pass branch | the pin's branch-exit and single-return assertions + the moved-block and no-return counter-examples |
| Dead code | `IsCarryingInParent` deleted with its last caller | grep over `src/` |
| Observability | the trace names all three treatments | `CarrySimulationTrace.ModeLabel` |

## 5. Red → green record

This session's tool harness unlocks the shell only at its verification stage, so the order was
implement → temporarily write the two adapter files back to HEAD → record the red → restore → run
green. What the red tree contained, exactly, because a decomposition that reads the arithmetic wrong
is the first thing a reviewer tries:

- `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs` and `Patches/BodyPatches.cs`
  were written back to HEAD's text, with ONE substitution so HEAD's shape compiles against the tree
  this cycle leaves behind: `CarriedBodyDriver.IsCarryingInParent(__instance)` (the helper this cycle
  deletes) became the surviving equivalent `CarriedBodyDriver.IsCarrying(__instance.body)`. Everything
  else stayed — the new rule, its matrix and the pin — which is why the pure rule matrix passes on that
  tree.
- Red, measured twice: **12 failed / 21 passed / 33** on the pre-review pin
  (`%TEMP%/cuo-red-carried-vitals.txt`) and **14 failed / 23 passed / 37** on the DELIVERED pin, after
  the review round widened the matchers (`%TEMP%/cuo-red2-carried-vitals.txt`) — the second is the
  record that describes this artifact. In both runs the 18 rule-matrix cases pass (the rule and its
  matrix are present on both trees); in the second run five pin cases pass as well, each for a reason
  that does not depend on the fixed file (`BodyFixedUpdatePatch`'s gate still reads
  `SkipsNativeSimulation` at HEAD, the pre-fix limb sample is rejected as it should be, and two samples
  are rejected because a file with no subset gate already fails their first check). The 14 failures are
  the contract itself — the vitals-subset block is not in the file, `Limb.Update` is not ruled for
  local bodies — plus the eleven samples whose mutation anchors exist only in the fixed file, which is
  the pin's own "the sample would silently no-op" guard rather than a vacuous pass.
- Green: the same filter on the restored tree = **37 passed / 0 failed**, exit 0
  (`%TEMP%/cuo-focused3-carried-vitals.txt`).

## 6. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The rule matrix and the mechanism pin pass | the focused filter above | 37 passed / 0 failed, exit 0 |
| The carry/proxy family did not regress | `--filter "FullyQualifiedName~Carr\|FullyQualifiedName~RenderProxy"` | 273 passed / 0 failed (`%TEMP%/cuo-family2-carried-vitals.txt`) |
| The repository gates hold | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` (unfiltered, with every required checklist box ticked) | 209 passed / 0 failed (`%TEMP%/cuo-gates-final2-carried-vitals.txt`) |
| The build is clean | `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors (`%TEMP%/cuo-build2-carried-vitals.txt`) |
| Formatting is clean | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, no file changed (`%TEMP%/cuo-format2-carried-vitals.txt`) |
| The whole suite runs green with the checklist checked | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` (with build) | 4054 main + 208 gate cases passed, exit 0 (`%TEMP%/cuo-full-carried-vitals.txt`) |

## 7. Adversarial review round

An independent reviewer (fresh context, frozen working tree; full report at
`%TEMP%/cuo-review-carried-unconscious-vitals.md`) returned **0 blocker / 2 major / 3 minor /
3 nit**: it could not falsify the mechanism — the three-way owner, the stage set and its order, the
freeze brackets and the "no change for the other populations" claim all survived its own reproduction
(it reflected the compiled `Treatment` over all 16 inputs and re-implemented the pin's matcher outside
the repository to run defect-injected copies). Both majors were in the new PIN, and every finding was
fixed in the same commit:

- **M1 — the pin's excluded-stage scan did not cover `HandleVisuals`** (named by its own expectation
  string, and the one exclusion whose violation is a real regression), so folding the presentation
  call into the "vitals" half still passed. Fixed by asserting the subset METHOD's exact body (which
  rejects any extra statement) plus a new counter-example that injects
  `HandleVisualsMethod.Invoke(...)`; that name is deliberately NOT in the file-wide scan, because it
  legitimately appears on the proxy path.
- **M2 — the stage check ran on the whole file, and the enclosing branch was never asserted**, so
  hoisting the stage calls out of `RunVitalsSubset` or moving the subset block into the whole-pass
  branch (where it would never run for the pinned body) both passed. Fixed: the stage assertions are
  scoped to the method's exact body, the file-wide count-== -one scan still catches hoisting, and the
  prefix now asserts that the gate sits on the skipped branch (the branch exit and its single
  `return true;`), with a counter-example for each of the two shapes.
- **m3 — the "16-combination matrix" was 13 rows.** Fixed: the matrix is now all 16 combinations with a
  length assertion; the reviewer's reflection of the compiled `Treatment` over the same inputs is
  recorded in §2.
- **m4 — "every predicate is a view of `Treatment`" was not true** of `RunsLimbSimulation` (one flag)
  and of the two `KeepsNativeSimulation` delegators. Fixed: `SuppressesMovement` and
  `CarrierOwnsGroundContact` are now literal `Mode.Full` views, `RunsLimbSimulation`'s doc says what it
  is, and the matrix asserts its agreement with the mode for every row.
- **m5 — the excluded-stage claim was broader than the pin.** Fixed with M1 and the §1/§4 cells
  reworded to say exactly what the pin enforces.
- **n6/n7/n8 — three wording defects**: `CarriedBodyDriver`'s "of that pass" had no antecedent (now
  names the native `Body.Update`); the `Ragdoll()` reading was true of the FIRST call site only (the
  `averagePain > 99` call site is not behind the `standing` guard, so the trailing freeze is
  load-bearing, not belt-and-braces — corrected here, in the ticket and in decision 227); and
  `RunsVitalsSubset`'s doc carried an adapter clause, now removed.
- **n9 — the limb gate's null-body shape**: recorded in §8 as a declared residual rather than patched,
  because the reviewer could not show it is reachable and the decompiled field is prefab-serialized.
  The reviewer's own statement of it was narrower than written: HEAD's gate also ran the pass for a
  body-less limb that was NOT carried (its `body != null &&` inputs made the rule answer "not
  skipped"), so only a body-less limb under a carried body changes, and such a limb would already
  throw inside its own `Update` on every other local body.

The reviewer stated its own limits: it had shell (every write went to `%TEMP%`, the repository was left
untouched) and used it for the pin run, the reflection and the mutation reproduction, but it ran no
build, no full suite, no format and no game — so the numbers in §5/§6 remain this cycle's own runs.

## 8. Limits

- The automated suite proves the decision, the stage contract and the exclusion set; it cannot see a
  rendering. The ticket's four acceptance rows — a bleeding unconscious teammate's vitals advancing, a
  carried corpse staying limp, the pair staying attached while the carrier moves, and an exact release
  — need a real two-client session on the deployed build.
- The five `MethodInfo` lookups resolve against the SHIPPED game assembly at runtime, not in the test
  host: a native method renamed by a game update surfaces as the patch class's own
  `InvalidOperationException` at plugin load, the same guard the existing `HandleVisuals` lookup uses.
  No test in this repository can see that resolution.
- A text pin cannot see whether the prefix runs at all, nor an early `return;` inserted in the prefix
  BEFORE the gate — both are runtime facts. Inside the subset method an early return IS caught, because
  that method's body is compared exactly.
- A `Limb` whose serialized `body` field were null would now run its own `Update` on a carried body
  where HEAD's gate skipped it (the pass dereferences `this.body` first, so it would throw). Recorded,
  not patched: HEAD's expression was not a guard either (for a body-less limb that was NOT carried its
  `body != null &&` inputs made the rule answer "run"), the field is prefab-serialized and assigned by
  the prefab, and a body-less limb would already throw inside its own `Update` on every other local
  body. No probe has shown the shape is reachable.
- On the pinned path the presentation half still neutralizes five pose inputs AFTER the subset ran
  (`attackRot`, `armOffset`, `moveDir`, `attackCooldown`, `eatTime`), so "one writer per frame" does not
  hold for those five fields there. Harmless (the subset only decays the first two), recorded rather
  than papered over.
- `HandlePeriodicChecks` also writes limb and held-item `localScale` on its 30 s roll and can drop a
  held item — the same writes the game performs for any body in that state, on a body whose pose the
  carry relation owns; no new wire traffic, since no capture scope wraps those paths.
- The trace's Information line records the treatment at ATTACH time: a body that loses consciousness
  later reports `native-simulation` on that line and `vitals-only` in the per-second frames after it.
  The distinction is visible; the first line is a snapshot, not a projection.
- The subset consumes `Random` exactly where the native stages do (`HandlePeriodicChecks`,
  `Limb.Update`): that is what the game does for a body in this state, but it is a change of this
  client's RNG stream relative to the frozen presentation.
- `HandlePeriodicChecks` can, on its own random rolls, start native coroutines and play local clips
  (vomit, cry, growl) — again what the game does for a body in that state. Nothing new rides the wire:
  no capture scope wraps those paths.
- Acceptance row 2 is read as PRESENTATION: a carried corpse's pose stays the pinned ragdoll and it
  never stands up. Its internal wound/infection numbers keep advancing exactly as they do for a body
  lying on the ground, because nothing in the game stops `Limb.Update` for a dead body. A reading of
  that row as "a corpse's numbers must freeze too" would be a re-ruling, not a bug in this cycle.
