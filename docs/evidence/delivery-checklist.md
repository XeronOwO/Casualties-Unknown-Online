# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the mine's explosion removes buildings, not the block table
- [ ] Native parity — the crate is the game's own `containercrate` prefab, removed by the game's own explosion
- [ ] Native parity probe — the census's `containercrate` group falls on the host and then on the member
- [ ] Entry mapping — the row enters through `trap-stage` + `trap-trigger` and leaves through each client's own `world-object-census`
- [ ] Mechanism inventory — the world batch's trap row is this one; `block-sync` already covers the terrain path
- [ ] Whole-family audit — the witness is per-client, so the member's own copy is judged, not the host's report
- [ ] Self-check table — build 0 errors, gates 834/0, suite 29 passed 0 failed
- [ ] Verification design — the row passes alone and inside the order
- [ ] Plan approved by the user — night mode: one row per slice, commit and push
- [ ] Structure review done — the case reuses the shared breakable-cell finder; no new helper duplicated
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
