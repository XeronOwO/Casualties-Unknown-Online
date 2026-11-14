# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the trigger chain is written with file:line before any edit
- [ ] Native parity — the game stamps the starting pod around each client's own body
- [ ] Native parity probe — the recorder now patches the stamper and logs structure, cell, gen, caller and player position
- [ ] Entry mapping — one write seam: `SetBlock` postfix → `WorldEventSync.OnBlockSet`
- [ ] Mechanism inventory — the remaining gap is the runtime caller that runs the stamper on one side only
- [ ] Whole-family audit — the pod and the three clearing circles are the same generation-relative family
- [ ] Self-check table — instrumentation only, no behaviour change
- [ ] Verification design — the `[Stamper]` line names structure, cell, genMode, caller and player position
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — the recorder stays acceptance-only, installed by `AcceptancePlugin`
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the deploy that reads the `[Stamper]` line follows in the next cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
