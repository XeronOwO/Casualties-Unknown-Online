# Middle-click location marker (circle → exclamation) self-check

Owner cycle: backlog "Middle-click location marker". Decision: implement a
CUO-native transient co-op ping as a dedicated one-shot presentation event over
the existing session star relay. KrokMP's raw packet, `NetPlayer` singleton
fields and hardcoded sprites are reference behaviour only — CUO uses a semantic
message, a Runtime-owned local ping buffer, and an IMGUI projection that needs
no new assets.

## Design decisions

- **Input**: Plugin-level middle-click (`Input.GetMouseButtonDown(2)`) is
  captured only while the local player is in an active session world and the
  pointer is not over a CUO UI/modal surface.
- **One marker per player**: a new middle click replaces that player's previous
  ping. A second click within 400 ms after a circle upgrades it to an
  exclamation and retargets it to the new cursor world position; after the
  window it starts a fresh circle.
- **Lifetime / fade**: 5 s total, with the final 1 s faded by the Runtime clock
  (`ITimeSource`) so both expiry and fade use the same monotonic seam.
- **Transport**: one dedicated bidirectional `LocationPing` message
  (`NetMsg 124`, `LocationPingMsg`) with star semantics — guest → host report,
  host fires the received event on its own client and relays to the other
  members (source excluded). No JToken/JObject channel, no snapshot, no
  authority/world state.
- **Presentation**: `LocationPingOverlay` projects the world position into GUI
  space; on-screen pings draw the circle/exclamation plus the pinger's name,
  off-screen pings pin a direction arrow to the screen edge with the same name.
- **Scope**: no mod-facing API in this slice. The feature is gameplay-adjacent
  UI only and stays inside Plugin/Runtime; `Abstractions` is not touched.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Input capture / UI guards | `LocationPingInputHandler.TryHandle` — middle click, session active + in-world, `OnlineUiOverlay.IsPointerOverUi` |
| 2 | Local ping domain | `LocationPingService` — one marker per SteamId, double-click window, lifetime, prune, session-end clear, invalid-kind/echo drop |
| 3 | Wire channel | `LocationPingChannel` + `WorldChannelRelay` + `WorldService`/`IWorldControl` — send/report/broadcast/fire |
| 4 | Packet handler | `LocationPingHandler` — fire local event, host relays except source |
| 5 | IMGUI rendering | `LocationPingOverlay.Draw` — on-screen marker or off-screen edge arrow, player color/name, fade alpha |
| 6 | Protocol direction | `DirectionTests.BidirectionalMessages` explicitly classifies `NetMsg.LocationPing` |

## 2. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Wire roundtrip | all `LocationPingMsg` fields survive encode/decode | `LocationPingSyncTests.LocationPing_RoundTripsEveryField` |
| Star relay | guest report fires on host and relays to the other guest | `LocationPingSyncTests.GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest` |
| Star relay | host's own ping reaches both guests | `LocationPingSyncTests.HostOwnPing_BroadcastsToBothGuests` |
| Star relay | relayed ping fires on the other guest | `LocationPingSyncTests.GuestRelay_FiresTheEventOnTheOtherGuest` |
| Source exclusion | reporting guest does not receive its own ping back | `LocationPingSyncTests.UnknownSender_IsNotEchoedToSource` |
| Local placement | first click creates a circle | `LocationPingServiceTests.FirstClick_CreatesCircleAndAddsLocalPing` |
| Double-click | second click within window upgrades to exclamation and retargets | `LocationPingServiceTests.SecondClickWithinWindow_UpgradesCircleToExclamationAndRetargets` |
| Double-click | click after window starts a new circle | `LocationPingServiceTests.SecondClickAfterWindow_StartsANewCircle` |
| In-world guard | no local placement outside a world | `LocationPingServiceTests.TryPlace_WithoutLocalInWorld_ReturnsFalse` |
| Expiry | expired pings are pruned | `LocationPingServiceTests.Prune_RemovesExpiredPings` |
| Session teardown | active pings clear on session end | `LocationPingServiceTests.SessionEnd_ClearsActivePings` |
| Echo rejection | a local-owner echo is not added | `LocationPingServiceTests.ReceivedLocalEcho_IsDropped` |
| Remote receive | a remote ping is added for UI projection | `LocationPingServiceTests.ReceivedRemotePing_AddsMarker` |
| Invalid kind | unknown enum value is dropped | `LocationPingServiceTests.ReceivedInvalidKind_IsDropped` |
| Host spoof guard | a claimed sender different from the transport sender is dropped on the host | `LocationPingServiceTests.HostSpoofedSender_IsDropped` |
| Direction guard | new message is explicitly bidirectional | `DirectionTests.EveryNetMsg_IsExplicitlyClassified` |

## 3. Verification

- Targeted tests: 15 passed / 0 failed.
- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — clean on source.
- `tools/check-architecture.ps1` — passed.
- `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` —
  no entity-event kind touched; gates run in the full commit pass.
- Development-period rule: L0 fake-network + static evidence; no manual
  dual-client acceptance.
