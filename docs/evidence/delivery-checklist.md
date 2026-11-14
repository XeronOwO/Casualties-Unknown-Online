# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the recorder patches the game's own `SetBlock` and names its caller
- [ ] Native parity — the game's writer is wrapped, not replaced; the write still happens
- [ ] Native parity probe — the recorder logs only the deep band, once per cell, to keep the file readable
- [ ] Entry mapping — installed by the acceptance plug-in only; a production install never carries it
- [ ] Mechanism inventory — the sweep count for the acceptance project moved 9 → 10
- [ ] Whole-family audit — the recorder is passive until the game writes; no batch arms it
- [ ] Self-check table — build 0 errors, gates 834/834
- [ ] Verification design — the next run reads the writer from the host's own log
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — one recorder class, one install line
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the deploy follows this commit

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
