# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a break the host applies changes its own world and must reach the member's block table
- [ ] Native parity — the reading is the game's own block table on both clients
- [ ] Native parity probe — `block-break-at` at a soil cell changed the host's block and the member read the same cell changed
- [ ] Entry mapping — the entry is the absolute cell read on each client (`block-read-at`)
- [ ] Mechanism inventory — the pod shell (block 14) has no `BlockInfo` row; the soil band does
- [ ] Whole-family audit — the parked row's reading is superseded by the passing case
- [ ] Self-check table — 22/22 green in one run
- [ ] Verification design — the case scans a ±4 band, breaks one cell, reads both sides
- [ ] Plan approved by the user — night mode: a sweep row converted, no product decision
- [ ] Structure review done — the case sits beside the others and uses the shared helpers
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
