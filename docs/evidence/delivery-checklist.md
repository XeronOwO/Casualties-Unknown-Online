# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the row's claim and the game's own fitting path are written in the rebuild ticket's row 25 section; the prosthetic is part of the limb's own state (`Limb.ProstheticInfo`).
- [x] Native parity — evidence: the row cuts an extremity through `Limb.Dismember` (`Limb.cs:127`) and fits the peg through the item's own `useLimbAction` (`Item.cs:745-785` → `Limb.AttachProsthetic`, `Limb.cs:97-122`).
- [x] Native parity probe — evidence: `.acceptance/prosthetic-2` PASS 1.8 s — the limb is reattached (`dismembered` false) and its prosthetic list names the peg.
- [x] Entry mapping — evidence: the fitting is reached the way the wound view reaches it: the carrier limb is selected and `PlayerCamera.ApplyWoundItem` is called with the granted item.
- [x] Mechanism inventory — evidence: `limb-dismember` (read/make) and `peg-fit` (the item's own action) are the two recipes; the case keeps them read-only about state.
- [x] Whole-family audit — evidence: the wire gap this row met is filed as its own ticket (`todo/1.0-the-wire-carries-no-prosthetic.md`) with the member-side half named as its consequence.
- [x] Self-check table — evidence: the three questions are answered in the ticket's row 25 section, including the overlay/clone half that waits on the wire.
- [x] Verification design — evidence: the row requires the game's own reattach and the limb's own prosthetic list, and names both readings on failure.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the amputation helper lets the game refuse per limb and returns the first real stump; the fitting stays one call in the game's own path.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-only change, so nothing to deploy; `.acceptance/prosthetic-2` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
