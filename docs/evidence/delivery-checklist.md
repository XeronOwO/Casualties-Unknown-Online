# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.

- [ ] Design comprehension
      reading, the run sheet and the residual, and the design page it cites (`docs/design/cross-cutting/observability.md`)
      is unchanged because the split moves no diagnostic.
- [ ] Native parity
      roll inside `RemoteBlockWrite`) still holds and the gates re-read it.
- [ ] Native parity probe
      `block-sync` row recorded on the ticket (`0.1.0+94f886c43`, case 1.0 s, both directions).
- [ ] Entry mapping
      README row moved from In progress to Done; no other page linked the ticket.
- [ ] Mechanism inventory
      counts `BlockBreakRemoteApply.cs`, so no other class is pinned to this ticket.
- [ ] Whole-family audit
      (`cycle-20261113-0336.md`, `cycle-20261118-0506.md`) were repaired to the done path, so the backlog-reference gate
      reads 836/836 again.
- [ ] Self-check table
      format exit 0 cover the tree this docs change sits on.
- [ ] Verification design
      host 0/0 and member 0/0), with the break crossing in both directions; the residual says the log-line comparison was
      not emitted and is not claimed.
- [ ] Plan approved by the user
      this cycle only records the reading and moves the ticket.
- [ ] Structure review done
      is written on the ticket rather than left in a message.
- [ ] Build + dotnet format + dotnet test normative gates pass
      reads "通过: 836" with exit 0; no source file changed in this cycle.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
