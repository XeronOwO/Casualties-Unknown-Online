# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the craft screen's own open path is the recipe's direct call; where it throws is now named
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — one recipe call, one failure reading
- [ ] Entry mapping — the entry is `PlayerCamera.OpenCraftScreen`
- [ ] Mechanism inventory — the recipe's own call is the suspect, not the CUO window
- [ ] Whole-family audit — the case stays parked with both readings
- [ ] Self-check table — both readings recorded
- [ ] Verification design — the next step is inside the recipe
- [ ] Plan approved by the user — night mode: a park with its cause, no product decision
- [ ] Structure review done — one registry comment switches the case off
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
