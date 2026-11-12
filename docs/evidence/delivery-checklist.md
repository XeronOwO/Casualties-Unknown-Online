# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the poll rules the tick out; the snapshot is the new candidate
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — the case's own poll is the probe
- [ ] Entry mapping — the entry is the console verb `/heal @a`
- [ ] Mechanism inventory — `Limb.ImpactDamage` leaves no ticking component
- [ ] Whole-family audit — the reset's landing and the persistent value are both recorded
- [ ] Self-check table — before/after reads and the 15 s poll are on the ticket
- [ ] Verification design — the next probe logs the limb writes and the applied report
- [ ] Plan approved by the user — night mode: the reading is filed, no product decision asked
- [ ] Structure review done — the candidate owner is the host's snapshot path
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
