# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the root cause read on the release decompile; no design changed
- [ ] Native parity — no native behaviour changed; the reading is the game's own filter call
- [ ] Native parity probe — no probe ran this cycle; the next probe is named on the ticket
- [ ] Entry mapping — the entry is the crafting panel's search field, read through `crafting-search`
- [ ] Mechanism inventory — the patch, the filter line and the locale lookups are named
- [ ] Whole-family audit — both branches (English literal, pinyin) are recorded
- [ ] Self-check table — the ticket records both readings and the decompile lines
- [ ] Verification design — one recipe call per branch, judged on `rowCount`
- [ ] Plan approved by the user — night mode: the finding is filed with three options and a recommendation
- [ ] Structure review done — no code changed; the fix belongs to the pinyin mod
- [ ] Build + dotnet format + dotnet test normative gates pass — documentation-only cycle

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
