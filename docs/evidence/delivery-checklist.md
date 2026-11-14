# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the divergence is data, not view: `GetBlock` reads `worldBlocks`, the Tilemap is a picture of it
- [ ] Native parity — the read path is the game's own `GetBlock`/`GetBlockDamage`
- [ ] Native parity probe — the differing rows and columns are named on the ticket
- [ ] Entry mapping — the data writers are listed with line numbers (`SetBlock`, the break relay, `GenerateObjectAtPos`)
- [ ] Mechanism inventory — the runtime placement callers and the player-anchored carving are named
- [ ] Whole-family audit — the ticket now says a write one client sees and the other does not is a defect, not a design choice
- [ ] Self-check table — the probes are ordered (idle, movement, writer)
- [ ] Verification design — gates 834/834 after the record
- [ ] Plan approved by the user — "no pragmatic workaround; locate the root cause and fix it"
- [ ] Structure review done — no source change this cycle
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
