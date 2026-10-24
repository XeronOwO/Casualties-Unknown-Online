# Acceptance — the middle-click location marker, read on two clients

- Ticket: `docs/backlog/done/middle-click-location-marker.md` — verdict: **every accepted note passes, the ticket
  moves to `done/`**
- Batch: `.acceptance/` (local, gitignored; this run is `ping*`)
- Artifact: deployed plugin `0.1.0+5bc7241c…` (verified before launch); two clients, host physical and guest in
  the sandbox, one lobby and one world
- Date: 2026-10-24
- Result: **the peer sees the sender's marker at the same position and type, a quick second placement upgrades
  and retargets it, and both clients prune it after the lifetime.**

## The rows

| Accepted note | Evidence |
|---|---|
| both host and guest see the same marker position and type | the guest (sender `…387957`) placed a circle at `(12.5, −3.5)`; the host's own domain read back exactly `{"sender":"…387957","x":12.5,"y":-3.5,"kind":"Circle"}`, and the guest's own read is the same record — one entry each, no duplicate echo |
| the double-press window upgrades and retargets, deterministically | two placements back to back (inside the 400 ms window) turned the marker into `kind":"Exclamation"` at the **new** position `(27.5, 13.5)` on both clients, replacing the circle |
| the marker expires | 7 s after the last placement both clients report `count:0` (lifetime 5 s, pruned on read) |
| no new generic wire, no Unity/game type crossing Abstractions | the code path is the dedicated `LocationPing` event and `ILocationPingControl`; this run drove the same `TryPlace` the middle-click handler calls |
| the tests cover wire roundtrip, relay, exclusion, upgrade, expiry, session end, echo drop, invalid kind, direction | the run's suites: **779 normative gates + 6,395 behaviour tests, 0 failures** |

## Limits and substitutions

- The placement was driven through `ILocationPingControl.TryPlace`, the production surface the middle-click
  handler calls after its own checks (session active, in world, pointer not over UI); the physical middle-click
  and the cursor→world conversion are the handler's input half and were not re-staged. The first probe call
  placed nothing because the guest was still finishing world entry — the domain's own guard, retried once the
  client reported `inWorld` and `role:Guest`.
- The marker is drawn by the CUO overlay; this run judges the marker domain both clients hold (the data the
  overlay renders), not a captured frame. The look of the marker stays the user's judgement, as the ticket's
  renderer note says.
- No second world or save state was used.
