# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the crafting screen's open path is the game's own OpenCraftScreen → RefreshRecipeList → Recipe.resultSprite
- [ ] Native parity — the recipe calls the game's own method and reads the game's own fields; nothing is replaced
- [ ] Native parity probe — the deployed build's throw names Recipe.get_resultSprite at RefreshRecipeList
- [ ] Entry mapping — the row enters through crafting-panel mode=open and leaves through its ok/panelActive/detail
- [ ] Mechanism inventory — Recipe.cs:10-20 loads the product prefab by result.id; a missing prefab makes GetComponent throw
- [ ] Whole-family audit — every visible recipe passes through the same refresh, so one bad product closes the whole panel
- [ ] Self-check table — build 0 errors, gates 834/0, the ticket's index row 156 chars
- [ ] Verification design — the throw was reproduced twice (open on an inactive panel, close then open) with the same stack
- [ ] Plan approved by the user — night mode: file the finding with its reading and move to the next row
- [ ] Structure review done — the recipe's catch reports the inner exception and its object state; no new helper
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
