# Delivery checklist

Fill every box, then run `tools/check-delivery.ps1`. Each box needs the literal ` — evidence: ` followed by the evidence.

- [x] Design comprehension — evidence: the removal claim, the host's displayed copy and the product's leave intent are written in the rebuild ticket's row 43 section.
- [x] Native parity — evidence: the host's own displayed copy is read by `remote-body-count mode=removal`, the leave goes through the product's `leave-world-intent`, and the host log's own `clone destroyed` line is required.
- [x] Native parity probe — evidence: `.acceptance/removal-1` PASS 26.4 s — copy present → absent, host log `clone destroyed`, and the member's own client left the world.
- [x] Entry mapping — copy present → `leave-world-intent` on the member → copy reaches 0 ≤ 5 s → host log trace → member state out of the world ≤ 10 s.
- [x] Mechanism inventory — `remote-body-count`, `leave-world-intent`, host log tail, member session state.
- [x] Whole-family audit — evidence: "judged on one side only" is the family shape; the member's own state is now the second half, and the roster half is named as residual.
- [x] Self-check table — evidence: three questions answered in the ticket's row 43 section (host-only, roster, player's own quit path).
- [x] Verification design — evidence: a red names the probe before/after, the log tail and the member's own state.
- [x] Plan approved by the user — evidence: the user's standing order is to rebuild every row from the native game and the machine, one row at a time.
- [x] Structure review done — evidence: the row drives and reads; the recipes only read counts and state.
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: units 6751/6751, gates 837/837, `dotnet format --verify-no-changes` exit 0.
- [x] Release-cycle deployment/acceptance — evidence: tests only, nothing to deploy; `.acceptance/removal-1` is the acceptance reading.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
