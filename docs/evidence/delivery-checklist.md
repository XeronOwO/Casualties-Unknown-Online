# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the container-give claim, the game's own `Body.PickUpItem` / `PlayBackpackSound` path and the receiver-side rule are written in the rebuild ticket's row 42 section.
- [x] Native parity — evidence: the clip is heard through the game's own sound recorder on both clients (`sound-log match=backpack`), and the transfer goes through the give probe into the bag's own instance id.
- [x] Native parity probe — evidence: `.acceptance/givecont-1` PASS 3.8 s — host `raw=523|` (silent), member `raw=525|backpack1@25.3;backpack1@25.3`.
- [x] Entry mapping — wound-free path: member carries a trash bag → world bandage copy → both recorders armed → `give-probe mode=give-world container=<bag instance>` → read both recorders ≤ 8 s.
- [x] Mechanism inventory — `give-probe`, `sound-log`, `bag-instance`, `body-items`, `item-provide`, `item-drop`, plus `teleport` for the pair.
- [x] Whole-family audit — evidence: the "either client" hole is the same shape as the chime rows; fixed by naming the receiving client, and the missing bag-contents assertion is called out as residual.
- [x] Self-check table — evidence: three questions answered in the ticket's row 42 section (`||` too wide, contents not asserted, fixture created).
- [x] Verification design — evidence: a red names both recorders and the member's bag, so the failure says which side was silent.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row reads; staging is done by separate recipes, so no recipe both stages a state and judges it.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests only, nothing to deploy; `.acceptance/givecont-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
