# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the sweep's table must name the cases that actually run
- [ ] Native parity — no product change
- [ ] Native parity probe — the table's claims match the registry
- [ ] Entry mapping — each batch names its cases
- [ ] Mechanism inventory — crafting is carried by `crafting`; the panel's open is parked
- [ ] Whole-family audit — the sounds batch now names both green cases
- [ ] Self-check table — the last full run read 23/23
- [ ] Verification design — the table's rows point at case names
- [ ] Plan approved by the user — night mode: a record correction, no decision
- [ ] Structure review done — one table edit, no code
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
