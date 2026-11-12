# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the host's log proves the chain on the host's side; the member's own trace is the gap
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — the host log is the probe
- [ ] Entry mapping — the entry is the console verb `/heal @a`
- [ ] Mechanism inventory — the applied handler, the store and the member's own body are named
- [ ] Whole-family audit — the host's chain and the member's missing trace are both recorded
- [ ] Self-check table — both logs are quoted
- [ ] Verification design — the case records the member's log tail beside the reading
- [ ] Plan approved by the user — night mode: the next probe is named, no product decision
- [ ] Structure review done — the probe uses the existing `log-tail` action
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
