# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.
- [ ] Design comprehension
- [ ] Native parity
- [ ] Native parity probe
- [ ] Entry mapping
- [ ] Mechanism inventory
- [ ] Whole-family audit
- [ ] Self-check table
- [ ] Verification design
- [ ] Plan approved by the user
- [ ] Structure review done
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: pending the post-commit deploy, `verify-deploy`, the targeted batch and the full 33-case batch

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
