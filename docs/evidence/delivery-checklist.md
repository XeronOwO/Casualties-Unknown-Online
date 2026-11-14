# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the reset's two ids answer whether it writes the displayed body
- [ ] Native parity — no product content; a live reading
- [ ] Native parity probe — five consecutive `console-heal` runs passed on the synced build
- [ ] Entry mapping — one entry, the host's `/heal @a`
- [ ] Mechanism inventory — the reset writes the displayed body, so the earlier failures came from the un-synced box
- [ ] Whole-family audit — every case's readings since the stale box are suspect; the suite's fresh pair reads its own build
- [ ] Self-check table — one ticket changed
- [ ] Verification design — a failing run must now compare the two ids
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the reading goes on the ticket, the launcher fix is its own slice
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
