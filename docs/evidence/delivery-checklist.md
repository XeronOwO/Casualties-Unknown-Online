# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the ghost role survives because the IP-direct service reports "still a lobby" after its transport ended; the teardown now asks the service's own activity
- [ ] Native parity — the IP-direct service hard-codes `IsInitialized` true and a host owner (`IpDirectSteamService.cs:24,44`), so neither fact can decide; its `IsActive` follows the transport
- [ ] Native parity probe — the teardown line read `role Guest, lobby 1, active False` after the previous condition, proving the old facts never decide on this path
- [ ] Entry mapping — the entries: `ISteamService.IsActive` (interface + three implementations + the test fake), the teardown branch, the suite's `role-clearing` case
- [ ] Mechanism inventory — the misreadings named: the cached lobby id is not the lobby, and the IP service's initialized/owner facts are constants
- [ ] Whole-family audit — the family is every `ISteamService` implementation: `SteamService` (live lobby), `IpDirectSteamService` (transport), `CuoNetworkRouter` (delegates), the fake (settable); the build is green across all
- [ ] Self-check table — build 0 errors, unit 6656/0 (one flaky red on a first run, green on the rerun), gates 834/0, format 0
- [ ] Verification design — after the deploy the `role-clearing` case must read `role None` on the member and join the host's second session without a restart
- [ ] Plan approved by the user — the user's main line is to finish the remaining tickets
- [ ] Structure review done — one interface member with a documented meaning, implemented by delegation; the decision point stays in the teardown
- [ ] Build + dotnet format + dotnet test normative gates pass — build 0 errors, unit 6656/0, gates 834/0, format 0

- [ ] Release-cycle deployment/acceptance: the role row is judged on the deployed build by this deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
