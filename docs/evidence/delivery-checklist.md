# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the reset runs on the target's own client and reports; the row judges the target's limb afterwards
- [ ] Native parity — the trace comes from the member's own BepInEx log through the driver's log-tail
- [ ] Native parity probe — the reset reported attempt 1 and the host stored applied true
- [ ] Entry mapping — the row enters through the host's /heal @a and leaves through the member's limb read
- [ ] Mechanism inventory — restoredLimbs counts re-attached limbs only, so 0 is not the defect signal
- [ ] Whole-family audit — the remaining suspect is the fixture's own wound seed landing after the reset
- [ ] Self-check table — gates 834/0, eleven suite runs read (seven green, one heal failure)
- [ ] Verification design — the failure is captured from a real run's report, not reproduced by hand
- [ ] Plan approved by the user — night mode: record the reading and the next probe, move on
- [ ] Structure review done — no code change; the ticket carries the trace and the fixture hypothesis
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
