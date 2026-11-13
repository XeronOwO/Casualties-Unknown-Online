# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — a transport-backed identity has no lobby that can outlive the session: the IP-direct "lobby" IS the TCP transport, so the session's end must end it; a Steam lobby outlives the session so a returning host rebuilds it
- [ ] Native parity — `IpDirectSteamService.cs:24` hard-codes `IsInitialized => true` and `:44` `GetLobbyOwner() => HostPeerId`, so neither fact can decide ownership; `LeaveLobby() => Disconnect()` (~:145) sets `_isActive = false` and fires `LobbyLeft`
- [ ] Native parity probe — the live teardown read `Session teardown finished: role Guest, lobby 1, active False.` with the transport still joined (`IsActive` true), proving neither `_currentLobbyId == 0` nor `!IsActive` can fire on the IP-direct end path
- [ ] Entry mapping — the entries: `ISteamService.EndsWithTheSession` (interface + `SteamService` false + `IpDirectSteamService` true + `CuoNetworkRouter` delegation + `FakeSteamService` settable), `SessionService.TeardownSession` (the new branch before the clearing condition), and the fact family `SessionRoleClearingTests` (4 facts)
- [ ] Mechanism inventory — the misreadings named: the cached lobby id is not a lobby, the IP service's initialized/owner facts are constants, and `LeaveLobby` is the only way the transport identity ends
- [ ] Whole-family audit — the family is every `ISteamService` implementation: `SteamService` (live lobby, keeps the role), `IpDirectSteamService` (transport, ends with the session), `CuoNetworkRouter` (delegates by the active transport), `FakeSteamService` (settable, counting-only leave); build 0 errors across all
- [ ] Self-check table — build 0 errors, unit `6658/0` (two interim reds were the fake's new side effect changing two returning-host tests; both reverted to their own contract), gates `834/0`, `dotnet format` no diff
- [ ] Verification design — after the deploy the `role-clearing` case must read `role None` on the member after the host leaves the session, then the member joins the host's second session without a restart
- [ ] Plan approved by the user — the user's main line is to finish the remaining backlog tickets (m48245's goal edit)
- [ ] Structure review done — one interface member with a documented meaning, delegation in the router, the decision kept in the teardown; the fake stays a counting stand-in so the returning-host tests keep their own contract
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, unit `6658/0`, gates `834/0`, format 0

- [ ] Release-cycle deployment/acceptance: the role row is judged on the deployed build by this deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
