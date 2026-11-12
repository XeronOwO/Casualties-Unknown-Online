# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a live client with a dead session is not what `EnsurePairAsync`'s relaunch repairs
- [ ] Native parity — the batch's fixture rule is now a `-Restart` cycle before a full run
- [ ] Native parity probe — the same tree read 22/5 then 27/27, so the difference is the pair, not the code
- [ ] Entry mapping — the reading is on the suite ticket beside the case list
- [ ] Mechanism inventory — documentation-only; no case or recipe changed
- [ ] Whole-family audit — the degraded-pair signature names both families it broke (UI rows and the world request)
- [ ] Self-check table — gates 834/0 this cycle
- [ ] Verification design — the recovery is judged by a full green run after the restart
- [ ] Plan approved by the user — night mode: a fixture lesson lands on its ticket and the queue continues
- [ ] Structure review done — the lesson is one ticket section, no new rule file
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
