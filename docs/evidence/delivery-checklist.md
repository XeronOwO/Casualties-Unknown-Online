# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the ticket's two halves closed on one reading: the guest stays a member with no world (18 s, past the old 15 s bound) and the host's gate releases into the world with it
- [ ] Native parity — no game content; the fix is the session's own heartbeat
- [ ] Native parity probe — the reading is each client's own `state` action, not a fixture assertion
- [ ] Entry mapping — the window's `home.transport.ip`/`ip.create_host`/`ip.join` and the driver's `start-run`
- [ ] Mechanism inventory — one mechanism: the guest's 15 s silence watchdog against a host that stops pinging outside an active session
- [ ] Whole-family audit — the same silence bound was already the cause of the role row's ghost; both rows now read on the same deployed build
- [ ] Self-check table — gates 834/0, language clean, ticket moved with its index and census rows
- [ ] Verification design — the row's own order (join, wait, start) is the check; the ticket's rows 1-2 are met and row 3 stays as it was read
- [ ] Plan approved by the user — the goal's main line: finish the remaining backlog tickets, one measured row at a time
- [ ] Structure review done — no code touch beyond the already-committed fix; the ticket moves to done with its status line and rows
- [ ] Build + dotnet format + dotnet test normative gates pass — gates 834/0, language clean

- [ ] Release-cycle deployment/acceptance: deployed `0.1.0+c6428d88e…` and read this cycle (the pre-run join, both clients' own state)

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
