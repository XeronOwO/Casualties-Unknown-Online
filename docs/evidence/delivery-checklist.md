# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a relay that reaches a generating world is dropped with no recovery; the fix holds it
- [ ] Native parity — the held relay is applied through the same apply path a whole world uses
- [ ] Native parity probe — the host's trace showed the send (`FlushPendingBlockBreak Committed(0+0)`) while the member's cell stayed stale
- [ ] Entry mapping — the entry is `WorldEventSync.BindToSession` → `BlockBreakSync.OnRemoteBlockDamaged`
- [ ] Mechanism inventory — the guest branch, the stale refusal and the bounded queue are the three shapes
- [ ] Whole-family audit — the recorded ceiling names its owning ticket
- [ ] Self-check table — build 0 errors, gates 834/0, unit 6650/0 this cycle
- [ ] Verification design — the `block-sync` case is the judge after this change
- [ ] Plan approved by the user — night mode: the fix follows the ticket's recommendation (buffer, apply when whole)
- [ ] Structure review done — one new top-level type file, the ceiling recorded with its owner
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
