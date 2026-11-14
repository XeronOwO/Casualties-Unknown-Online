# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the ticket's root cause is corrected to our own example mod's recipe
- [ ] Native parity — the vanilla game opens the crafting screen; the mod layer is the difference
- [ ] Native parity probe — the deployed build opened the panel (`panelActive: true`) with one placeholder line and no NullReferenceException
- [ ] Entry mapping — the craft key → OpenCraftScreen → RefreshRecipeList → Recipe.resultSprite chain is written on the ticket
- [ ] Mechanism inventory — one getter feeds every list refresh; the patch is owned by the Crafting capability
- [ ] Whole-family audit — the fix moved with the ticket into `done/` with its index and census rows
- [ ] Self-check table — the README row moved to Done and the census path follows
- [ ] Verification design — gates 834/834 after the move; the reading carries its numbers
- [ ] Plan approved by the user — the user ordered the fix
- [ ] Structure review done — no source change in this cycle
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the fix was deployed and read in the previous cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
