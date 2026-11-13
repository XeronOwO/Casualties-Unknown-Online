# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a wait belongs in the client that owns the fact, not in a caller's sleep loop
- [ ] Native parity — reads `IWorldPresenceQuery` in the same process
- [ ] Native parity probe — the action answers at the menu with reached=false and the full budget consumed
- [ ] Entry mapping — the script's action set and switch carry `wait-state`; the driver dispatches it
- [ ] Mechanism inventory — the command name, the argument (the key) and the text (the budget) are the whole contract
- [ ] Whole-family audit — `wait-log`, `log-tail` were already custom actions; they are now listed in the script's set too
- [ ] Self-check table — two tooling files; no product code
- [ ] Verification design — the suite's conversion to this action comes next; the action itself is proven by the live call
- [ ] Plan approved by the user — the goal's main line (event-driven)
- [ ] Structure review done — one action, one predicate, one answer shape
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
