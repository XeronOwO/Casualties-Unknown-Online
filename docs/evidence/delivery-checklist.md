# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the start-run wait runs in the host, not in a 750 ms sleep loop
- [ ] Native parity — the driver reads `IWorldPresenceQuery` in the game's own process
- [ ] Native parity probe — the driven run entered the world through the new wait
- [ ] Entry mapping — one call site, the start-run gate
- [ ] Mechanism inventory — the budget stays under the eval timeout (20 s against 30 s)
- [ ] Whole-family audit — the join gate still polls; it converts next
- [ ] Self-check table — one test file
- [ ] Verification design — the full suite is the judge
- [ ] Plan approved by the user — the goal's main line (event-driven)
- [ ] Structure review done — a fallback poll kept only behind the blocking wait
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
