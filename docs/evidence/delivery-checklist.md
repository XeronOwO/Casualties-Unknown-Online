# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the recipe channel caches a failed compile per recipe name, so one bad edit poisons every mode
- [ ] Native parity — no game content touched this cycle; the recipe was reverted
- [ ] Native parity probe — the failing calls' own answers are the evidence
- [ ] Entry mapping — one entry, the driver's recipe action
- [ ] Mechanism inventory — one mechanism: a per-name compiled-recipe cache in the client, invalidated only by a restart
- [ ] Whole-family audit — the earlier `select-character` "no value" had the same shape (an edit the channel could not take)
- [ ] Self-check table — tree reverted, only the ticket line changed
- [ ] Verification design — the next attempt restarts the host after the edit, then calls the recipe once
- [ ] Plan approved by the user — the goal's main line: the suite carries the regression
- [ ] Structure review done — a reading, not a fix
- [ ] Build + dotnet format + dotnet test normative gates pass — no code change; gates last green

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
