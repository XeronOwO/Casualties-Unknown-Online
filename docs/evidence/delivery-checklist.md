# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — five tickets carried the user's decisions of 2026-11-14
- [ ] Native parity — no code changed; the decisions are recorded on the tickets
- [ ] Native parity probe — the pinyin limit is the measured `[Pinyin] extended match` line on zh-CN
- [ ] Entry mapping — the two moved tickets are `git mv` renames with their census rows updated
- [ ] Mechanism inventory — the README index rows moved to Future and Done
- [ ] Whole-family audit — the surviving three rows keep their review rows and the decision lines
- [ ] Self-check table — the census paths match the new folders
- [ ] Verification design — the normative gates run on the moved tickets
- [ ] Plan approved by the user — the decisions were given in the answer batch of 2026-11-14
- [ ] Structure review done — no source touched
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: not part of a documentation cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
