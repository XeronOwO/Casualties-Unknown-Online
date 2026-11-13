# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the removal case's marker is item-side, so the case never saw the displayed body it judges
- [ ] Native parity — no game content; the probe now separates clones carrying a `Body` from the rest
- [ ] Native parity probe — the probe's own answer is the evidence
- [ ] Entry mapping — one entry, the suite's probe recipe
- [ ] Mechanism inventory — one mechanism: `RemoteCloneRender` marks cloned items, not the player body
- [ ] Whole-family audit — `clone-probe` (the other clone case) reads the same probe and passes, so the probe answers
- [ ] Self-check table — suite builds 0 errors; `--list` no longer names `remote-removal`
- [ ] Verification design — the case is parked with its reading until the body-clone instrument exists
- [ ] Plan approved by the user — the goal's main line: the suite carries the regression
- [ ] Structure review done — a probe field and a parked case; no product change
- [ ] Build + dotnet format + dotnet test normative gates pass — suite build 0 errors, gates 834/0

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
