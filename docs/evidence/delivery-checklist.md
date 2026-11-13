# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the start-run note carries the host's own wait so a slow entry is visible in the report
- [ ] Native parity — the number comes from the driver's own `waitedMs`
- [ ] Native parity probe — the driven run entered the world and passed every case
- [ ] Entry mapping — one return, one note
- [ ] Mechanism inventory — the note is a string only; no judgement reads it
- [ ] Whole-family audit — the other gates keep their own notes
- [ ] Self-check table — one test file
- [ ] Verification design — the full suite is the judge
- [ ] Plan approved by the user — the goal's main line (event-driven)
- [ ] Structure review done — the note stays inside the existing return
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
