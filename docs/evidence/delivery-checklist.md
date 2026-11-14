# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the domain page was read before the code
- [ ] Native parity — the reconcile reuses the existing apply path
- [ ] Native parity probe — the entry snapshot is the only host→member send today
- [ ] Entry mapping — host role, session active, in-world members only
- [ ] Mechanism inventory — the two live shapes (break, placement) plus the new differential step
- [ ] Whole-family audit — both directions now heal
- [ ] Self-check table — one new class, one service field, one pump call, one diff
- [ ] Verification design — 5 unit facts on the clock/baseline/diff plus the live `block-idle-drift` row
- [ ] Plan approved by the user — add it, optimize it, keep deterministic generation as the root fix
- [ ] Structure review done — the diff lives in its own class because the service hit the 600-line gate
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deploy and read the reconcile on the pair

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
