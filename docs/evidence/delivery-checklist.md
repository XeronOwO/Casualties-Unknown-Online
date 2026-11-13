# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the session end keeps the Role so a returning host can rebuild the same lobby; IP-direct has no lobby event, so a kept role becomes a ghost
- [ ] Native parity — not a game action: this is CUO's own identity state, so the parity question is which lobby owns the identity
- [ ] Native parity probe — the ghost was read on the boxed guest before the fix: `role=Guest, inWorld=false, page=Home`, `leave-world` answers without changing the role
- [ ] Entry mapping — `EndSession()` → `TeardownSession(leaveLobby: false)` → the new branch; a guest's watchdog calls the same `EndSession`
- [ ] Mechanism inventory — the Role is assigned in four places (host lobby create, host lobby enter, guest lobby enter, and reset on lobby leave); the new branch is the only one that clears it for a transport with no lobby
- [ ] Whole-family audit — every consumer of `Role` (12 call sites across the adapter) reads it as "whose authority am I", so a cleared role after a dead session is the correct answer for all of them
- [ ] Self-check table — build 0 errors; unit suite 6652/0 (two new facts); normative gates 834/0 after the new ticket's Design section landed; `dotnet format` ran
- [ ] Verification design — the two unit facts judge the branch (a lobby keeps the role, no lobby clears it); the live row is the boxed guest rejoining a new session without a restart
- [ ] Plan approved by the user — the user ordered a root-cause fix instead of the fixture's forced restart; the ticket carries the options and the recommendation
- [ ] Structure review done — no new type; one branch and one log line in the session end path; the file stays under its ceiling
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
