# State-stream bandwidth reduction — recipient-local player echo removal

Owner cycle: backlog `docs/backlog/todo/state-stream-bandwidth-reduction.md`
(moved to Review after this cycle). The network traffic baseline
(`docs/backlog/review/network-traffic-baseline.md`) measured the high-frequency
`PlayerStateStream` as a per-payload family on every host→guest frame. This
cycle removes one provable redundant part of that stream: the host was echoing
a guest's own player entry back to that same guest on every 20 Hz broadcast.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Host player broadcast | `PlayerStreamExchange.BroadcastPlayerState` previously built one `WireStateStream` with the host + every synced guest and sent the same list to every guest (`src/.../Session/EntitySync/PlayerStreamExchange.cs`). |
| 2 | Guest local state source | A guest already knows its own local state; the guest->host report carries it and the local `PlayerEntity` is written from the local simulation (`EntitySyncService.PublishLocalState`). |
| 3 | Stream receive semantics | `PlayerStreamExchange.OnEntityStateStreamReceived` applies incoming state by entity id; omitting the recipient's own entry does not remove any entity (lifecycle is owned by PlayerJoin/PlayerLeave). |
| 4 | Existing seq gate | Each recipient still receives a monotonic `Seq` per stream, so the unreliable-stream drop gate is unchanged. |

## 2. Changes

- `PlayerStreamExchange.BroadcastPlayerState` now builds one stream per
  recipient and omits that recipient's own player entry via
  `BuildPlayerStreamList(synced, target.SteamId)`.
- No wire/protocol change: same `WirePayloadType.PlayerStateStream`, same
  `WireStateStream` shape, same unreliable delivery.
- The host and every other member's states are still included, so remote clones
  continue rendering all other players.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Per-recipient stream omits own entry | `BuildPlayerStreamList` skips `entities.LocalPlayer.SteamId == excludeSteamId` and each matching `SyncedEntity.SteamId` | `StateStreamBandwidthTests.HostPlayerStream_OmitsRecipientOwnState_ButKeepsOthers` |
| Other players remain visible | The stream still contains the host entity and the other guest | same test asserts host state is received by both guests |
| Frequency/cadence unchanged | Frames are still sent at the configured cadence; only per-frame payload content changes | existing `StateStreamFrequencyTests` still green |
| No wire contract break | No `NetMsg`, `WirePayloadType`, `WireStateStream` or protocol version edits | code diff is Runtime/GameAdapter/tests only |

## 4. Verification results (2026-09-05)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| Focused runtime/regression tests | `StateStreamBandwidthTests`, `StateStreamFrequencyTests`, `NetworkTrafficBaselineTests`, `RemoteBackpackContractTests`, `CloneInventoryContentSanitizerTests`, `RemoteProxyDragPolicyTests` all pass |
| `dotnet format` | clean |
| Architecture/event-replay/entity-event/delivery gates | pass |

## 5. What was NOT changed (and why)

- No delta encoding: this cycle only removes the provably redundant per-owner
  echo. A full field-delta stream remains a later optimization and needs its own
  measurement/design.
- No frequency reduction: 20 Hz is also a reliability/fallback cadence; reducing
  it would change client interpolation behavior, not just bandwidth.
