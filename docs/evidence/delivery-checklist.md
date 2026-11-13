# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the row closed on its own reading: `role-clearing` PASS on `0.1.0+9ac538ca9`, half one `role None, active false, hostId 0` after the host left, half two `role Guest, inWorld true` after the join
- [ ] Native parity — no game content; the chain is the router's liveness answer and the transport's read-loop ownership, both read on the pair
- [ ] Native parity probe — the acceptance reading is the game's own state action on both clients, not a fixture's assertion
- [ ] Entry mapping — the window's controls (`home.leave`, `ip.join`) and the suite's `state`/`click`/`start-run` entries only
- [ ] Mechanism inventory — the two fixes named on the ticket (stale read-loop cleanup, stale selection), with the earlier misreadings superseded
- [ ] Whole-family audit — the "old edge ends the new session" shape was fixed at the transport; the "stale selection" shape at the router; both read live
- [ ] Self-check table — gates 834/0 after the move, language clean, format no diff, build unchanged
- [ ] Verification design — the row's own case is the check; a regression reopens it
- [ ] Plan approved by the user — the goal's main line: finish the remaining tickets, one measured row at a time
- [ ] Structure review done — the ticket moves to done with its index row (Done section), census row and status line; nothing else changes
- [ ] Build + dotnet format + dotnet test normative gates pass — gates 834/0, language clean, format 0

- [ ] Release-cycle deployment/acceptance: deployed and read this cycle (`role-clearing` PASS on the pair)

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
