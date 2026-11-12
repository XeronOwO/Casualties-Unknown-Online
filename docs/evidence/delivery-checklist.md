# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the filter's own string is the question; the probe answers it without guessing
- [ ] Native parity — the patch touches only the game's `string.Contains` inside the refresh scope
- [ ] Native parity probe — one line per distinct query names the instance the matcher receives
- [ ] Entry mapping — the entry is the crafting panel's search field
- [ ] Mechanism inventory — the refresh, the filter line, the locale lookups and the patch are named
- [ ] Whole-family audit — both branches (English literal, pinyin) are read by the same probe
- [ ] Self-check table — the ticket records the readings, the decompile lines and the options
- [ ] Verification design — deploy, query `cuiruo`, read the probe line and the row count
- [ ] Plan approved by the user — night mode: a diagnostic line only, no product decision
- [ ] Structure review done — the probe is one static field and one log line inside the existing patch
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
