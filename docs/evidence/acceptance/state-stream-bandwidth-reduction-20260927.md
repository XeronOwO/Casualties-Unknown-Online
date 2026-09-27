# Acceptance record — State-stream bandwidth reduction

- Ticket: `state-stream-bandwidth-reduction` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This ticket carries no acceptance section, so its `## What landed` claims are enumerated one by one.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The redundant per-recipient player-state echo is removed: the host builds one stream per recipient and omits the recipient's own entry while still sending the host and every other member. | machine | pass | `StateStreamBandwidthTests.HostPlayerStream_OmitsRecipientOwnState_ButKeepsOthers` is `Passed` in the main suite (4482/4482, 0 not executed): it collects the `WireStateStream` frames each of two guests receives and asserts `DoesNotContain` that guest's own entity and `Contains` the host's entity for both guests; the "every other member" half is carried by row 2's code inspection (`BuildPlayerStreamList` adds every synced member except the excluded id) |
| 2 | `PlayerStreamExchange.BroadcastPlayerState` now writes per-recipient streams via `BuildPlayerStreamList(synced, target.SteamId)`. | machine | pass | `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs` `BroadcastPlayerState` loops the synced members and sends each target its own `WireStateStream` whose `PlayerStates = BuildPlayerStreamList(synced, target.SteamId)`, addressed to `[target.SteamId]`; `BuildPlayerStreamList` adds the local player when it is not the excluded id and skips the member whose `SteamId` equals `excludeSteamId` |
| 3 | No wire/protocol change: same `WirePayloadType.PlayerStateStream`, `WireStateStream` shape, unreliable delivery and sequence gating. | machine | pass | the same send site rides `WirePayloadType.PlayerStateStream` with `reliable: false` and stamps `Seq = ++_nextStateSeq`; the guest receiver returns early on `stream.Seq <= LastStateSeq`, and each host-side report keeps its own `LastReportSeq` space; the regression test's collector still decodes the frame as `WirePayloadType.PlayerStateStream` |
| 4 | Regression test: `StateStreamBandwidthTests.HostPlayerStream_OmitsRecipientOwnState_ButKeepsOthers` verifies both guests never receive their own entity and still receive the host. | machine | pass | `Passed` in the main suite; the test's four assertions are exactly the two per-guest `DoesNotContain` (own entity) and the two per-guest `Contains` (host entity), with `NotEmpty` guards on the collected streams and states |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe. Rows 1 and 4 are decided by the named `StateStreamBandwidthTests` case; rows 2 and 3 by direct inspection of `PlayerStreamExchange.cs`. The ticket's "the host previously sent every guest a full roster list" half is the pre-change state and was not re-run here — what this batch decides is the current send-side behaviour and the payload/delivery facts above. The bandwidth effect is observed as per-recipient list membership and not as a measured byte count: no traffic measurement exists in this batch, and the ticket's own selfcheck is the place where any earlier count lives. Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`. Every path cited above exists in the tree.
