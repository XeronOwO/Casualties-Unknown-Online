# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the case reads the data (`GetBlock`), not the view
- [ ] Native parity — the same game API on both clients
- [ ] Native parity probe — 36 rows differ, first at row 985; neither client changed over 45 s idle (0 and 0)
- [ ] Entry mapping — the case runs after the suite's own join, both bodies in world
- [ ] Mechanism inventory — the reading rules out autonomous simulation and points at a one-off write
- [ ] Whole-family audit — the case is the family's own instrument and stays after the investigation
- [ ] Self-check table — the case builds and answers a named row on failure
- [ ] Verification design — gates 834/834 with the new case in the tree
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — one case, one reading, no product change yet
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
