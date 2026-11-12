# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a reading recorded against the plugin's own matching path; no design changed
- [ ] Native parity — no native behaviour changed; the reading is the game's own panel filter
- [ ] Native parity probe — the panel was driven through the game's own search field
- [ ] Entry mapping — the entry is the crafting panel's search field, read through `crafting-search`
- [ ] Mechanism inventory — the plugin's filter path, the panel's field, and the log lines are named on the ticket
- [ ] Whole-family audit — the English branch (`flimsy`) and the pinyin branch (`cuiruo`) were both read
- [ ] Self-check table — the ticket records both readings and the plugin's own log lines
- [ ] Verification design — one recipe call per branch, judged on `rowCount`
- [ ] Plan approved by the user — night mode: the finding is filed with its options, no product decision asked
- [ ] Structure review done — no code changed; the ticket names the plugin's matching path as the next read
- [ ] Build + dotnet format + dotnet test normative gates pass — documentation-only cycle; the previous cycle's gates read 834/0 at `11b12e8f5`

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
