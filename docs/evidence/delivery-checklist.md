# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the crafting panel's own entry is `PlayerCamera.OpenCraftScreen`; the attempt names where it throws
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — one recipe call, one failure reading
- [ ] Entry mapping — the entry is the game's own craft screen
- [ ] Mechanism inventory — the CUO window is named as the likely precondition
- [ ] Whole-family audit — the case is parked with its reading, not deleted
- [ ] Self-check table — the ticket records the reading
- [ ] Verification design — the next attempt closes the window before the open
- [ ] Plan approved by the user — night mode: a parking with its cause, no product decision
- [ ] Structure review done — the case stays beside the others and is switched off by one comment
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
