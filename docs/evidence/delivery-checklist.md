# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the row judges the treated body after the reset, so it waits for the member's own reset line
- [ ] Native parity — the trace is the member's own BepInEx log line through the driver's `log-tail` action
- [ ] Native parity probe — the member reports the applied reset to the host and the host stores it
- [ ] Entry mapping — the row enters through the host's `/heal @a` and leaves through the member's own limb read plus its own trace line
- [ ] Mechanism inventory — `restoredLimbs` counts re-attached limbs only, so 0 is not the defect signal; the remaining suspect is the fixture's seed landing after the reset
- [ ] Whole-family audit — the member relaunch is a fallback that is now counted and named in the report, so a ghost world reads as a finding instead of a silent recreate
- [ ] Self-check table — build 0 errors, gates 834/0, `dotnet format` ran, four consecutive `--suite console-heal` runs read PASS
- [ ] Verification design — the healthy pair read `member the suite joined the member to the host's session` with no relaunch trigger
- [ ] Plan approved by the user — night mode: the reading and the next probe are recorded, the queue continues
- [ ] Structure review done — test-only slice: the case's judgement moved behind the trace poll, the driver gained one counter
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
