# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the case now records the member's own trace beside the reading
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — the trace is the probe
- [ ] Entry mapping — the entry is the console verb `/heal @a`
- [ ] Mechanism inventory — the retry path is named as the intermittent's likely home
- [ ] Whole-family audit — the member trace and the limb read are both recorded
- [ ] Self-check table — the ticket carries both readings
- [ ] Verification design — the next failing run's trace names the lost attempt
- [ ] Plan approved by the user — night mode: the probe is added, no product decision asked
- [ ] Structure review done — the probe uses the driver's existing `log-tail` action
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
