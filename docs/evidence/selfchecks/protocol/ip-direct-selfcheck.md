# IP direct connection (non-Steam transport) — self-check (2026-08-23)

Backlog §"Networking / transport candidate" asked for a second transport/identity
path: host/join by IP:port directly, bypassing Steam P2P, with a custom in-game
display name and a separate mode that is not interconnected with Steam sessions.
This slice lands that feature.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Transport contract | `INetworkTransport` remains the single message primitive; the new TCP path implements the same surface. |
| Session control plane | The existing `SessionService` state machine is reused unchanged — the IP path is presented to it through the `ISteamService` lobby adapter (host is logical peer id 1). |
| Star network | The existing handshake / `PlayerJoin` / host-relay flow is unchanged; IP-direct peers use synthetic 64-bit logical ids instead of SteamIDs. |
| Custom names | `HandshakeMsg`, `HandshakeAckMsg`, `PlayerJoinMsg` carry additive optional display-name fields; `MemberPresence` stores them for UI/roster rendering. |
| UI | Home page adds IP host/join controls; Players/Network/member rows use the custom names; a top-left network HUD shows live RTT and delayed session-status text. |
| Config | `[IpDirect]` BepInEx entries: `ListenPort`, `JoinAddress`, `JoinPort`, `DisplayName`. |

## 2. Changes

- `IpDirectTransport` — TCP listener/connector, length-prefixed framing
  (`[int32 length][payload]`), transport-level 8-byte hello so each guest
  carries a random logical peer id; host id is always `1`. Reliable/unreliable
  are not distinguished (TCP is reliable — a safe degradation for the first
  slice). Disconnect notifications are drained on the main thread.
- `IpDirectSteamService` — `ISteamService` adapter that presents the TCP
  session as a lobby: `StartHost`/`Connect`/`Disconnect` fire the existing
  lobby events, `GetLobbyMembers` reads active TCP peers, `GetPersonaName`
  returns the configured custom display name / stored member names.
- `CuoNetworkRouter` — single DI seam that exposes the active pair
  (Steam or IP-direct) through both `INetworkTransport` and `ISteamService`,
  so `PacketSender`, `PacketReceiver` and `SessionService` need no mode
  branches.
- `IpDirectActions` (Plugin) — host/join/leave actions with the same
  session-in-world guards as the Steam lobby paths.
- `IpDirectConfigEditor` (Plugin) — owns the BepInEx config entries for
  listener/join address/port and custom display name.
- `HandshakeMsg` / `HandshakeAckMsg` / `PlayerJoinMsg` — additive protobuf
  fields for display names (`ProtocolVersion` stays 40: additive fields are
  not breaking wire changes).
- `MemberPresenceTable` — `DisplayName` per member.
- UI drawers + network HUD — IP host/join, custom-name rendering, top-left
  RTT + delayed status/notification text.
- Tests — real loopback TCP transport tests, IP steam-adapter tests, and two
  full-container end-to-end handshake tests.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1299 passed / 0 failed (full suite) |
| `tools/check-architecture.ps1` | passed |
| IP transport L0 tests | `HostAndGuest_ExchangeFrames_WithLogicalPeerIds`, `GuestDisconnect_RemovesPeerFromHostActiveList` |
| IP adapter L0 tests | `HostAndGuest_ReportIdentityAndCustomNames`, `Disconnect_RaisesLobbyLeftAndClearsMembers` |
| Full-stack IP-direct tests | `HostAndGuest_CompleteThreeLegHandshakeOverTcp`, `HostAndGuest_CarryCustomDisplayNamesThroughHandshake` |
| Protocol | ProtocolVersion 40 (additive fields only, no new NetMsg) |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `IpDirectTransportTests` — two real TCP transports on loopback complete a
  bidirectional frame exchange using logical peer ids, and a guest disconnect
  removes the peer from the host's active list.
- `IpDirectSteamServiceTests` — host/guest adapters report the correct roles,
  lobby membership/owner, custom names, and the lobby-left event on disconnect.
- `IpDirectSessionIntegrationTests` — two full Runtime containers connected
  over real loopback TCP complete the three-leg handshake and carry custom
  display names through the existing handshake/player-join flow.

## 5. Structure review

- The TCP transport is a narrow data-plane mechanism with no game/session
  knowledge (`IpDirectTransport`).
- The IP identity adapter is a thin `ISteamService`; the router is the only
  piece that knows which mode is active.
- IP action logic lives in `IpDirectActions`, not in the BepInEx lifecycle
  class, keeping `Plugin` under the architecture gate.
- No `SessionService` behavior work was added beyond one handshake field;
  the session state machine remains the same owner/star topology.
