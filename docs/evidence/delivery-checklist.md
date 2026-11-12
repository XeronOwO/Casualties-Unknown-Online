# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the world census reads the host's own generated world; no design changed
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — the case ran alone and inside the full suite
- [ ] Entry mapping — the world domain's entry is the host's own generated world
- [ ] Mechanism inventory — one reading per case, named in the report
- [ ] Whole-family audit — the new case plus the console-heal recurrence were both read this cycle
- [ ] Self-check table — the ticket records both readings
- [ ] Verification design — one recipe call, judged on `total ≥ 1`
- [ ] Plan approved by the user — night mode: a new case and a parking, no product decision
- [ ] Structure review done — the case is a small class beside the others, using the shared context
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
