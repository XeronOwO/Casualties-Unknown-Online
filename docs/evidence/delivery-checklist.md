# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the heal's reset runs on the target's own client and reports back
- [ ] Native parity — the ticket is a reading of the product's own relay
- [ ] Native parity probe — the case passes on a pair the suite owns and in the full order
- [ ] Entry mapping — the entry is `/heal @a` through the driver's console action
- [ ] Mechanism inventory — the earlier red was a stale member, not the product
- [ ] Whole-family audit — the ticket moved to Done with both readings kept
- [ ] Self-check table — README row and census row point at the new path
- [ ] Verification design — the suite's case is the standing judge
- [ ] Plan approved by the user — night mode: a ticket closed from its own reading
- [ ] Structure review done — one ticket closed, no code changed
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
