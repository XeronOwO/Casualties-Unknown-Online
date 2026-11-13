# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the leave wait runs in the client that owns the world, not in a caller poll
- [ ] Native parity — the driver reads `IWorldPresenceQuery` in-process
- [ ] Native parity probe — the suite's own leave paths used the new wait
- [ ] Entry mapping — two call sites replaced, one helper
- [ ] Mechanism inventory — the budget stays at 20 s under the 30 s eval timeout
- [ ] Whole-family audit — `inWorld` and `notInWorld` are the only predicates the driver answers; the rest still poll
- [ ] Self-check table — one tooling + one test file
- [ ] Verification design — the full suite is the judge
- [ ] Plan approved by the user — the goal's main line (event-driven)
- [ ] Structure review done — a named helper, no inline lambda duplication
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
