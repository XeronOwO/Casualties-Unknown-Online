# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the member's `/heal @a` instruction arrives and `LocalBodyReset` runs, yet the limb keeps its damage
- [ ] Native parity — the reset writes `HealResetState`'s limb values through `ApplyHealState`, the same path the game's own heal uses
- [ ] Native parity probe — the host's console answered `applied: true` while the member's own body stayed damaged, so the host's copy and the member's body disagree
- [ ] Entry mapping — the ticket is reopened because two full-suite runs read the same failure on the suite's own pair
- [ ] Mechanism inventory — the ticket now names the three suspects: which fields `ApplyHealState` skips on a damaged limb, whether the seeded wound's component re-applies, and the report retry
- [ ] Whole-family audit — the row's family is the medical batch; its siblings (`medical-chart`, `medical-dose`) stay green
- [ ] Self-check table — gates 834/0 this cycle
- [ ] Verification design — the row is judged by the member's own `limb-read` after the host's command
- [ ] Plan approved by the user — night mode: a finding is filed with its reading and the queue continues
- [ ] Structure review done — the ticket moved back to `review/`, its index row and census row follow it, and the Done row is removed
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
