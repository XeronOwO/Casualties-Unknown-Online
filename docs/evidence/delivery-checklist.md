# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the domain pages were read before the claim
- [ ] Native parity — the claim comes from our own code, not the vanilla build
- [ ] Native parity probe — the direction that exists and the one that is missing are named
- [ ] Entry mapping — the ticket names every entrance of the table
- [ ] Mechanism inventory — the game's own capacity bounds the reconcile
- [ ] Whole-family audit — the member→host direction and the host→member direction are both covered
- [ ] Self-check table — documentation only, no code changed
- [ ] Verification design — four acceptance rows written, each with its reading
- [ ] Plan approved by the user — periodic sync as the backstop, deterministic generation stays the root fix
- [ ] Structure review done — the backstop is named as a backstop, never a replacement
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
