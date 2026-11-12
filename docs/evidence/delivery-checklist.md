# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the console's registry and buffer are the service the UI reads; the row asks the service, not a copy
- [ ] Native parity — `block-sync` breaks a cell through the game's own `WorldGeneration` path and reads it back through the same one
- [ ] Native parity probe — the earlier suite run read `no breakable cell` at `(0, 496)`: 504 scanned cells were air and the pod's shell
- [ ] Entry mapping — `console-completion` enters through the driver's recipe path on the host's own client
- [ ] Mechanism inventory — the slice touches one new case, one scan and one poll
- [ ] Whole-family audit — the new row's family is the console/session batch; its other rows already pass or are parked with readings
- [ ] Self-check table — build 0 errors, gates 834/0, suite 25/25 this cycle
- [ ] Verification design — the row is judged by the service's own answers and by the member's own log line
- [ ] Plan approved by the user — night mode: the sweep row conversion is the goal's own queue
- [ ] Structure review done — one new case file, no new type outside `Cases/`, the registry line added
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
