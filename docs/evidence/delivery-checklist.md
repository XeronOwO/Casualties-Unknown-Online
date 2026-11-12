# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the run's clock is the host's and a member's request reaches it through the production `WorldTimeRequest` send
- [ ] Native parity — the speed is the game's own `PlayerCamera.curTimeScale`, the field the HUD's icon row reads
- [ ] Native parity probe — the host-side `world-time-request` never sends (`sent: false`, role `Host`); only a guest's request is sent
- [ ] Entry mapping — the row enters through `IWorldTimeControl.SendRequest` on the member and reads both clients' `time-scale-read`
- [ ] Mechanism inventory — the slice adds one case; nothing else changed
- [ ] Whole-family audit — the row's family is the save batch's world-time rows; its sibling rows already pass or are named
- [ ] Self-check table — build 0 errors, gates 834/0 this cycle
- [ ] Verification design — the row is judged by both clients' own speed fields after a request and after the restore
- [ ] Plan approved by the user — night mode: the sweep row conversion is the goal's own queue
- [ ] Structure review done — one new case file, no new type outside `Cases/`, one registry line
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
