# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the reopened ticket names the reset's own order against the wound tick
- [ ] Native parity — no native behaviour changed
- [ ] Native parity probe — the reading is the two limb reads from the same run
- [ ] Entry mapping — the entry is the console verb `/heal @a`
- [ ] Mechanism inventory — `LocalBodyReset.Reset`'s order and the wound tick are named
- [ ] Whole-family audit — the recurrence is recorded beside the earlier pass
- [ ] Self-check table — the ticket carries the before/after limb reads
- [ ] Verification design — the case re-un-parks only when the reset clears the wound state
- [ ] Plan approved by the user — night mode: a finding reopened with its reading
- [ ] Structure review done — the fix belongs to `LocalBodyReset`, not the fixture
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
