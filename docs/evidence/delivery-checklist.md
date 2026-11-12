# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the panel's refresh walks every recipe and reads its product sprite, so one missing prefab closes the whole screen
- [ ] Native parity — the probe reads the game's own static list and the game's own getter; nothing is replaced
- [ ] Native parity probe — the failing id is example.dressing with a NullReferenceException
- [ ] Entry mapping — the row enters through crafting-panel mode=probe and leaves through its badIds list
- [ ] Mechanism inventory — Recipe.cs:18 loads the product prefab by id; the recipe's own id has no loadable prefab in 1.0
- [ ] Whole-family audit — 137 of 138 recipes load; the single bad entry is the one the panel's refresh hits first
- [ ] Self-check table — build 0 errors, gates 834/0
- [ ] Verification design — the probe names the recipe instead of inferring it from a stack; the ticket carries both readings
- [ ] Plan approved by the user — night mode: file the finding with its reading and move to the next row
- [ ] Structure review done — the probe lives in the existing recipe's mode branch; no new file kept
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
