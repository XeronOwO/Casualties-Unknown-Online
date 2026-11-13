# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the cycle's row is a session end without a lobby event: the host clicks leave-session, the member's own state must read role None
- [ ] Native parity — the game's own session end path is `SessionService.TeardownSession` (`SessionService.cs:522-567`); the clearing branch at `:560-564` requires `Role != None && _currentLobbyId == 0`
- [ ] Native parity probe — the member's own log names the teardown and the kept role: `Session ended (role Guest kept).` with no `No lobby owns this session — role cleared.`
- [ ] Entry mapping — the entries used: the host's own `home.leave` control, the member's `state` action through the suite's driver, and the member's own log line
- [ ] Mechanism inventory — the misreading named: the first attempt used `leave-world` and expected a session end; a world leave keeps the session alive (`active: true, role: Guest`), which is a different row
- [ ] Whole-family audit — the family is "a session that ends with no lobby owner": IP-direct has no lobby callback at all, so the branch's `_currentLobbyId == 0` condition is the one to read next; no second entry is left untested in this cycle
- [ ] Self-check table — the case runs on the deployed build (`fdacf27fc`-era) and fails at its first requirement; the failure is the finding, not a fixture error
- [ ] Verification design — the next probe names both values at the teardown (role and `_currentLobbyId`) in one log line; the fix is not re-opened until that reading exists
- [ ] Plan approved by the user — the user's main line is to finish the remaining tickets, and this is one of them
- [ ] Structure review done — no `src/` change in this cycle: the case is a test file, the launcher fast path is a tool script; the suite builds and the case runs
- [ ] Build + dotnet format + dotnet test normative gates pass — gates 834 passed, 0 failed, exit 0

- [ ] Release-cycle deployment/acceptance: the role row reads None only after the next probe's fix

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
