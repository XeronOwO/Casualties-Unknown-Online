# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the host detects the break and flushes one BlockDamaged report; the member applies it
- [ ] Native parity — both logs are the product's own trace channel
- [ ] Native parity probe — the host's `FlushPendingBlockBreak result=Committed(0+0)` proves the send
- [ ] Entry mapping — the entry is `WorldGeneration.DamageBlock` → `OnBlockDamaged` → `FlushPendingBlockBreak`
- [ ] Mechanism inventory — the member's own log has no remote-apply line for the cell
- [ ] Whole-family audit — the finding is on the divergence ticket with both sides quoted
- [ ] Self-check table — the timestamps and op ids are quoted
- [ ] Verification design — instrument the member's remote-apply branch next
- [ ] Plan approved by the user — night mode: a finding with its evidence, no product decision
- [ ] Structure review done — no code changed this cycle
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
