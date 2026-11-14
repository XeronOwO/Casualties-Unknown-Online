# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the suite is the record; a reading goes on its ticket
- [ ] Native parity — no product content; a live run
- [ ] Native parity probe — 31 cases, all PASS on a synced pair
- [ ] Entry mapping — one command, `--suite all`
- [ ] Mechanism inventory — the box sync is the fixture rule that makes the readings trustworthy
- [ ] Whole-family audit — every box launch now syncs
- [ ] Self-check table — one ticket changed
- [ ] Verification design — the full suite is the judge
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the reading names the cause and the shape
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
