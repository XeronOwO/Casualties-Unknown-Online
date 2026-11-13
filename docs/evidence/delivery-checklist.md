# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a session no lobby can rebuild must leave no role; the branch now treats three facts as "no lobby owns this": no cached id, Steam not initialized, or no live owner
- [ ] Native parity — Steam's own answer for a lobby it does not own is 0 (`SteamService.GetLobbyOwner`), and a client with Steam off cannot own a lobby at all
- [ ] Native parity probe — the teardown line read `role Guest, lobby 1, active False` twice, the second time after the first fix, which is why the uninitialized-Steam fact is now part of the condition
- [ ] Entry mapping — the entries: `EndSession`/`OnLobbyLeft`, the teardown branch, the two unit facts, the suite's `role-clearing` case
- [ ] Mechanism inventory — the three misreadings named: leaving only the world keeps the session; the cached lobby id is not the lobby; a Steam call on a client with Steam off can answer an owner for a stale id
- [ ] Whole-family audit — the family is "session ends, who owns it": the same-lobby outage row and the no-lobby row, both in `SessionRoleClearingTests` 2/2
- [ ] Self-check table — build 0 errors, gates 834/0, format exit 0
- [ ] Verification design — after the deploy the `role-clearing` case must read `role None` on the member and join the host's second session without a restart
- [ ] Plan approved by the user — the user's main line is to finish the remaining tickets
- [ ] Structure review done — one condition and one log line; no new type or dependency
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, gates 834/0, format 0

- [ ] Release-cycle deployment/acceptance: the role row is judged on the deployed build by this deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
