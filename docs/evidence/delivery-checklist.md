# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — read `docs/design/game/worldgen-and-layers.md` and the generation-boundary implementation before the claim
- [ ] Native parity — the game consumes Unity's global `Random`; our mechanism captures and replays `Random.state` at the boundary
- [ ] Native parity probe — the `[GenStream]` fingerprints are identical on both clients
- [ ] Entry mapping — generation writes through `SetBlock`; the recorder logs the chain
- [ ] Mechanism inventory — the "unseeded RNG" hypothesis is withdrawn; the divergence is post-generation
- [ ] Whole-family audit — 96 common cells carry different values, 170/181 are one-sided, all in the pod's neighbourhood
- [ ] Self-check table — documentation only, no code changed
- [ ] Verification design — the next probe compares per-cell write history with timestamps
- [ ] Plan approved by the user — "先反思，更新规范" and "read our own docs first"
- [ ] Structure review done — the wrong section is marked superseded, kept as the record
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
