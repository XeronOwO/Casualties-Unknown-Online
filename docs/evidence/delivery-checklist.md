# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the isolation's log line and the game's loading text were read
- [ ] Native parity — the stage name is the game's own text
- [ ] Native parity probe — the two clients' segment 17 is eight bytes apart
- [ ] Entry mapping — the log runs at every generation save
- [ ] Mechanism inventory — the seasonal branch is excluded (11-14 is not Halloween)
- [ ] Whole-family audit — the probe adds one field to the existing segment line
- [ ] Self-check table — one diagnostic line, no behaviour change
- [ ] Verification design — the next real run reads `stage=` on both clients at segment 17
- [ ] Plan approved by the user — locate the root cause, no workaround
- [ ] Structure review done — the probe lives in the isolation that already owns the boundary
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deploy and read the stage field

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
