# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the panel's open ends in `RefreshRecipeList`, which reads the recipes the mods hook
- [ ] Native parity — the throw's surface is named in the game's own code
- [ ] Native parity probe — the open throws even with the CUO window closed
- [ ] Entry mapping — the entry is `PlayerCamera.OpenCraftScreen` → `RefreshRecipeList`
- [ ] Mechanism inventory — two mods read the same recipe list; the next step is a bisect
- [ ] Whole-family audit — the row stays parked with its narrowed cause
- [ ] Self-check table — the throw names the call chain
- [ ] Verification design — bisect the mods' hooks on the next attempt
- [ ] Plan approved by the user — night mode: a park with its cause, no product decision
- [ ] Structure review done — no code changed this cycle
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
