# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the probe answered what the filter sees; no design changed
- [ ] Native parity — no native behaviour changed this cycle
- [ ] Native parity probe — the rebuilt mod ran on the host and logged the filter's own string
- [ ] Entry mapping — the entry is the crafting panel's search field
- [ ] Mechanism inventory — the filter's input (English ids), the missing filter pass for `cuiruo`, and the panel's display name are named
- [ ] Whole-family audit — both queries were read on the same session
- [ ] Self-check table — the ticket carries both readings and the probe line
- [ ] Verification design — one recipe call per query, judged on `rowCount` and the log
- [ ] Plan approved by the user — night mode: the finding is filed with its halves and the fix's entry point
- [ ] Structure review done — the fix belongs to the refresh path, not the string comparison
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
