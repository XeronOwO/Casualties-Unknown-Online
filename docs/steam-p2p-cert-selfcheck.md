# Steam P2P Cert / Rendezvous Fix and Diagnostics (#118)

#118 was a recorded Steam P2P failure: `SendMessageToUser` intermittently
reports `Remote_BadCert` / rendezvous-style errors and self-heals later.
Investigation found **two layers**:

1. A **CUO transport usage defect**: the old send path used only
   `k_nSteamNetworkingSend_Reliable` / `Unreliable`. Per the
   ISteamNetworkingMessages contract, when a session is broken (peer close,
   cert error, rendezvous failure, any connection disruption), a send without
   `k_nSteamNetworkingSend_AutoRestartBrokenSession` returns
   `k_EResultNoConnection` until the caller explicitly calls
   `CloseSessionWithUser`. CUO never did either, so a broken P2P session could
   stay broken across retries until it was idle long enough for Steam to time
   the session out.
2. The **external/observability layer**: the actual `BadCert` / rendezvous
   causes are often local proxy or Steam client/relay state, but the old log
   did not label the failure family.

This cycle fixes the transport flag and adds the diagnostic classifier.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Every Steam P2P send goes through `SteamTransport.SendTo` → `SteamNetworkingMessages.SendMessageToUser` | SteamTransport.cs:27-56 |
| 2 | The old send used only `Reliable` or `Unreliable`, without `AutoRestartBrokenSession` | SteamTransport.cs (pre-change), Steamworks docs in `isteamnetworkingmessages.cs` |
| 3 | A broken session without the auto-restart flag returns `k_EResultNoConnection`; the caller must call `CloseSessionWithUser` or add the flag and retry | `SteamNetworkingMessages.SendMessageToUser` docs |
| 4 | A failed send already calls `LogSendDiagnostics` with the session state and `m_eEndReason` debug string | SteamTransport.cs:58-73 |
| 5 | Steamworks distinguishes `Remote_BadCert`, `Misc_P2P_Rendezvous`, `Local_ManyRelayConnectivity`, and timeout/connect/no-connection results | Steamworks.NET `ESteamNetConnectionEnd` / `EResult` |
| 6 | The host warm-up pump already backs off failed sends (1 s → 10 s cap) and resets on success | PeerWarmupBackoff.cs:35-58 |
| 7 | The guest handshake deliberately retries every 1 s | HandshakeHandler.cs:109-133, HandshakeAckAckHandler.cs |
| 8 | Local-machine history: Clash on `localhost:7890` has produced `4003 Bad cert` / `5008 rendezvous timeout` probabilistically | AGENTS.local.md network/notification notes; backlog #118 diagnostic order |

## 2. Design

- `SteamSendFlags.For(reliable)` — the single send-flag source: keeps
  `Reliable` or `Unreliable` and ORs in
  `k_nSteamNetworkingSend_AutoRestartBrokenSession` for every send.
  `AutoRestartBrokenSession` is a no-op on healthy sessions; it only affects a
  send that would otherwise hit a broken session.
- `SteamSendFailureKind` — compact failure families: `BadCert`, `Rendezvous`,
  `ConnectFailed`, `NoConnection`, `Timeout`, `Other`.
- `SteamSendFailureClassifier.Classify(EResult, ESteamNetConnectionEnd)` — pure
  mapping from the Steamworks pair to one family.
- `SteamSendFailureClassifier.Remediation(kind)` — one human-readable sentence
  per family, with the local proxy/Steam-client first for cert/relay families.
- `SteamTransport.LogSendDiagnostics` now logs:
  `SendMessageToUser to <id> failed: <result> (<kind>); session state: ..., end reason: ..., debug: "..."; <remediation>`.
- No new retry loop, no proxy workaround, no wire change.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Reliable sends | Reliable + AutoRestartBrokenSession | SteamSendFlagsTests.ReliableSend_IncludesAutoRestartBrokenSession |
| Unreliable sends | Unreliable + AutoRestartBrokenSession | SteamSendFlagsTests.UnreliableSend_IncludesAutoRestartBrokenSession |
| Flag hygiene | opposite reliability bit never leaks | SteamSendFlagsTests.Flags_DoNotLeakTheOppositeReliabilityBit |
| Broken-session recovery | next send automatically restarts instead of staying on `k_EResultNoConnection` | SteamSendFlags.For; Steamworks docs |
| P2P send result path | failed sends still return false; the flag changes the session-restart behavior on the next retry | SteamTransport.cs |
| Warm-up backoff | unchanged — the flag is transport-level, no retry-policy change | PeerWarmupBackoff.cs |
| Handshake retry | unchanged | HandshakeHandler.cs, HandshakeAckAckHandler.cs |
| BadCert mapping | `Remote_BadCert` → `BadCert` | SteamSendFailureClassifierTests.BadCert_EndReason_IsClassifiedAsBadCert |
| Rendezvous mapping | `Misc_P2P_Rendezvous` / `Local_ManyRelayConnectivity` → `Rendezvous` | SteamSendFailureClassifierTests.Rendezvous_* |
| Result fallbacks | ConnectFailed / NoConnection / Timeout / Other | SteamSendFailureClassifierTests |
| Remediation quality | every family has a non-empty string; BadCert names the proxy first | SteamSendFailureClassifierTests |
| Protocol | no wire/protocol/state change | no NetMsg additions; ProtocolVersion unchanged |

## 4. Verification

- L0 tests: 9 `SteamSendFailureClassifierTests` + 3 `SteamSendFlagsTests`; full
  build + suite green.
- Static evidence: Steamworks send-flag contract, enum names, and the existing
  retry paths cited above.
- Runtime evidence: development-period rule — L0 simulation + static evidence,
  no manual acceptance. The next real dual-side pass should see broken-session
  sends recover on the retry instead of staying on `k_EResultNoConnection`
  until idle timeout.

## 5. What was NOT changed (and why)

- No new application-level retry/backoff inside `SteamTransport`: the transport
  now uses Steam's own auto-restart flag; higher layers still own their retry
  cadence.
- No automatic proxy/network workaround: modifying local network config from
  the plugin would be outside CUO's responsibility and would not survive a
  user's proxy choice.
- No protocol version bump: no wire format or message semantics changed.
