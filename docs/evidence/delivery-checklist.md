# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a host's block break is reported to the member's own table
- [ ] Native parity — the reading is the game's own block table on each client
- [ ] Native parity probe — the host read `blockAfter 0` at `(4, 503)`; the member still read `block 2`
- [ ] Entry mapping — the entry is the absolute cell on both clients
- [ ] Mechanism inventory — the row passed earlier in the night, so the report reaches the member only sometimes
- [ ] Whole-family audit — the lapse is recorded on the divergence ticket, not hidden
- [ ] Self-check table — both readings quoted with their absolute cell
- [ ] Verification design — read the host's own break report beside the member's table on a failing run
- [ ] Plan approved by the user — night mode: a finding filed with its reading, no product decision
- [ ] Structure review done — the case stays in the order and is not weakened
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
