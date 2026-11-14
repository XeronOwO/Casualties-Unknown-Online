# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the domain pages were read before the claim
- [ ] Native parity — the per-cell write history comes from both clients' own logs
- [ ] Native parity probe — the generation streams match and the last writer differs per cell
- [ ] Entry mapping — the chain names `WorldGenerateStructures` and `WorldPlacePlayer`
- [ ] Mechanism inventory — the body position at the pod segment is the remaining variable
- [ ] Whole-family audit — every differing cell lies in the pod neighbourhood
- [ ] Self-check table — documentation only, no code changed
- [ ] Verification design — the next reading logs the body position at the pod segment on both clients
- [ ] Plan approved by the user — locate the root cause, no workaround
- [ ] Structure review done — the wrong RNG section is marked superseded and kept
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
