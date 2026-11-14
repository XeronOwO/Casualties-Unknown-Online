# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the SetBlock seam is already patched; the write is captured on the host
- [ ] Native parity — the capture path is the mod's own, the writer is the game's
- [ ] Native parity probe — the host's diff table is updated via `ReportBlockState`
- [ ] Entry mapping — the open question moved to live delivery, not the entry point
- [ ] Mechanism inventory — the generation-relation guard that could refuse a live report is named with its lines
- [ ] Whole-family audit — the next probe reads the member's log and `ReportBlockState`'s send path
- [ ] Self-check table — no code changed in this cycle
- [ ] Verification design — the probe names the branch before a message is added
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — the record is the next slice's input
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: no deploy in this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
