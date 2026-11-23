# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 28's claim, the game's splint script and its heal timer are written in the rebuild ticket's row 28 section with `SplintLimb.cs` and `Item.cs` lines.
- [x] Native parity — evidence: the fracture is the game's own `Limb.BreakBone`, the splint is applied through the same `ApplyWoundItem` the wound view's drop runs, the heal is `SplintLimb.Update`'s own timer, and the removal is the wound view's `WoundSpecialAction`.
- [x] Native parity probe — evidence: `.acceptance/splint-2` PASS 26.8 s — the break's own timer falls visibly faster with the splint on than off, the applied splint is consumed, and the removal returns it with no more condition than it was worn with.
- [x] Entry mapping — evidence: the apply uses the wound view's own apply path and the removal its own special action; the drag itself is named as not drivable because it reads the native cursor.
- [x] Mechanism inventory — evidence: `limb-break` (game fracture), `item-provide` (fixture item), `select-limb`, `limb-use mode=use` (apply seam), `limb-use mode=special` (removal), and the extended `limb-bleeding` `boneTimer`.
- [x] Whole-family audit — evidence: the fresh-body fallback was removed and replaced by a sentence that names what the row needs, matching row 27.
- [x] Self-check table — evidence: three questions answered in the ticket's row 28 section (the flag was judged instead of the healing; the returned item by existence only; the apply was not a player's drag).
- [x] Verification design — evidence: failures name both decay rates per two seconds, the worn condition against the returned one, and the consumed-item probe.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: applying, removing and reading stay separate one-call recipes, so no recipe both stages and judges.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/splint-2` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
