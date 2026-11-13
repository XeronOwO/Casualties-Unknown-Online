# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the ghost role is a session that ends with no lobby able to rebuild it; the branch now asks both the cached id and the live Steam owner
- [ ] Native parity — Steam reports no owner for a lobby id of 0 (`SteamService.GetLobbyOwner` returns 0 when `CurrentLobbyId == 0`), which is the live fact behind the change
- [ ] Native parity probe — the deployed teardown line read `role Guest, lobby 1, active False`: the IP-direct path carries a non-zero cached id, so the old `_currentLobbyId == 0` never held
- [ ] Entry mapping — the entries: the session-end path (`EndSession`/`OnLobbyLeft`), the teardown branch, the unit facts, the suite case
- [ ] Mechanism inventory — the two misreadings named: leaving only the world keeps the session; the cached lobby id is not the lobby (it read 1 with no Steam lobby)
- [ ] Whole-family audit — the two test rows cover the family: same-lobby end keeps the role, no-lobby end clears it; both pass
- [ ] Self-check table — unit 6656 passed / 0 failed, gates 834/0, format exit 0, build 0 errors
- [ ] Verification design — after the deploy the suite's `role-clearing` case must reach `role None` on the member and join the host's second session without a restart
- [ ] Plan approved by the user — the user's main line is to finish the remaining tickets
- [ ] Structure review done — one condition and one log line in an existing branch; no new type or dependency
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, unit 6656/0, gates 834/0, format 0

- [ ] Release-cycle deployment/acceptance: the role row is judged on the deployed build by this deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
