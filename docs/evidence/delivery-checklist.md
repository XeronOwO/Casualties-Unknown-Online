# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the dose row's claim, the game's own use formula (300 × clamp01(ml × 0.1)) and the member's delivery line are written in the rebuild ticket's row 41 section.
- [x] Native parity — evidence: the wound is seeded through `Limb.ImpactDamage`, the view is opened by the product's remote-medical path, the release runs the cream's own native limb action, and the effect is read from the member's own limb.
- [x] Native parity probe — evidence: `.acceptance/dose-3` PASS 2.0 s — the adapter measured 10.00 ml on limb 1 and the member's limb marker went 0 → 299.748, the value that volume implies.
- [x] Entry mapping — evidence: wound seed → wound view on the member's limb → `remote-medical-release` → the host log's `[MedicalView] measured … limb N` line and the member's own `local body received … (limb N)` line.
- [x] Mechanism inventory — evidence: `wound-seed`, `limb-read` (both sources), `remote-gesture` (wound-view), `remote-medical-release`, `medical-open`, `item-provide`, host log tail.
- [x] Whole-family audit — evidence: the same "a number exists" hole was fixed for the dose size; the panel's own limb row is named as residual because the chart row covers it.
- [x] Self-check table — evidence: three questions answered in the ticket's row 41 section (marker floor, dose size, declared teleport).
- [x] Verification design — evidence: a red names the measured volume, the value it implies and the marker the limb carries.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row reads and judges; the release and the seed are separate recipes, so no recipe both stages a state and judges it.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-recipes change only, nothing to deploy; `.acceptance/dose-3` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
