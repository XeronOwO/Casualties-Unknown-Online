# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: row 59b's claim, the game's own death path (brain damage at zero kills the body, no revive) and the fault sheet are in the rebuild ticket.
- [x] Native parity — evidence: fatal brain damage now has to leave the game's own body dead before the count is read, and the fresh-start half reads zero before any death.
- [x] Native parity probe — evidence: `.acceptance/deaths-2` PASS 25.4 s — the member's body read `alive:false`, `/deaths` read 1 on the member and 1 on the host, and both read 1 again after the save/continue.
- [x] Entry mapping — evidence: the death is the game's own brain-damage death, the count is read through the console the player uses, and the continue is `continue-run` plus a member rejoin.
- [x] Mechanism inventory — evidence: `body-force` (fatal brain damage), `body-read` (the game's own alive), `/deaths` on both clients, `continue-run`, the existing cut wait.
- [x] Whole-family audit — evidence: the revive and second-death steps are removed from every path in this row, and the comment says why a second death is a second character.
- [x] Self-check table — evidence: three questions answered in the ticket — the death is the game's, the count is tied to the body, and the impossible revive is gone.
- [x] Verification design — evidence: failures print the body reading and both clients' counts, so "counted without a death" names itself.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine.
- [x] Structure review done — evidence: the death is staged by one recipe call and the reading is by another; the row owns only the assertions.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6761/6761, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests-and-docs change only; `.acceptance/deaths-2` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
