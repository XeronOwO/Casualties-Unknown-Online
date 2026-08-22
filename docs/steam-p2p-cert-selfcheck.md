# Steam P2P Cert / Rendezvous Diagnostic Self-Check (#118)

The backlog item was a recorded-but-uninvestigated steam P2P failure:
`SendMessageToUser` intermittently fails with `Remote_BadCert` /
rendezvous-style errors and then self-heals when the link goes idle. This
cycle investigates with static/Steamworks evidence but finds **no CUO code
defect**: the known local triggers are the Clash proxy (`localhost:7890`) and
Steam client/relay state, both outside the plugin. The shipped change is an
actionable diagnostic classifier plus tests so a future occurrence can be
located from the log line without an interactive Steam session.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Every Steam P2P send goes through `SteamTransport.SendTo` → `SteamNetworkingMessages.SendMessageToUser` | SteamTransport.cs:27-56 |
| 2 | A failed send already calls `LogSendDiagnostics` with the session state and `m_eEndReason` debug string | SteamTransport.cs:58-73 |
| 3 | Steamworks distinguishes `Remote_BadCert`, `Misc_P2P_Rendezvous`, `Local_ManyRelayConnectivity`, and timeout/connect/no-connection results | Steamworks.NET `ESteamNetConnectionEnd` / `EResult` |
| 4 | The host warm-up pump already backs off failed sends (1 s → 10 s cap) and resets on success | PeerWarmupBackoff.cs:35-58 |
| 5 | The guest handshake deliberately retries every 1 s — a lost ack caused by a transient cert/rendezvous error is the designed recovery path | HandshakeHandler.cs:109-133, HandshakeAckAckHandler.cs |
| 6 | Local-machine history: Clash on `localhost:7890` has produced `4003 Bad cert` / `5008 rendezvous timeout` probabilistically | AGENTS.local.md network/notification notes; backlog #118 diagnostic order |

The transport had no label for the failure family — a log reader had to know
Steamworks enum values and the local proxy history. No retry policy was added
because the existing handshake/warm-up retries already self-heal after the
external cause disappears; the change is observability, not a second retry path.

## 2. Design

- `SteamSendFailureKind` — compact failure families: `BadCert`, `Rendezvous`,
  `ConnectFailed`, `NoConnection`, `Timeout`, `Other`.
- `SteamSendFailureClassifier.Classify(EResult, ESteamNetConnectionEnd)` — pure
  mapping from the Steamworks pair to one family.
- `SteamSendFailureClassifier.Remediation(kind)` — one human-readable sentence
  per family, with the local proxy/Steam-client first for the two cert/relay
  families.
- `SteamTransport.LogSendDiagnostics` now logs:
  `SendMessageToUser to <id> failed: <result> (<kind>); session state: ..., end reason: ..., debug: "..."; <remediation>`.
  No new wire message, no retry behavior change, no protocol bump.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| P2P send result path | unchanged — failed sends still return false; only the log is richer | SteamTransport.cs:45-73 |
| Warm-up backoff | unchanged — the classifier does not alter retry decisions | PeerWarmupBackoff.cs |
| Handshake retry | unchanged | HandshakeHandler.cs, HandshakeAckAckHandler.cs |
| BadCert mapping | `Remote_BadCert` → `BadCert` | SteamSendFailureClassifierTests.BadCert_EndReason_IsClassifiedAsBadCert |
| Rendezvous mapping | `Misc_P2P_Rendezvous` / `Local_ManyRelayConnectivity` → `Rendezvous` | SteamSendFailureClassifierTests.Rendezvous_* |
| Result fallbacks | ConnectFailed / NoConnection / Timeout / Other | SteamSendFailureClassifierTests |
| Remediation quality | every family has a non-empty string; BadCert names the proxy first | SteamSendFailureClassifierTests.BadCertRemediation_*, EveryKind_HasARemediation |
| Protocol | no wire/protocol/state change | no NetMsg additions; ProtocolVersion unchanged |

## 4. Verification

- L0 tests: 9 new `SteamSendFailureClassifierTests` cover the mapping and
  remediation strings; full build + suite green.
- Static evidence: Steamworks enum names and the existing retry paths cited
  above.
- Runtime evidence: development-period rule — L0 simulation + static evidence,
  no manual acceptance. The new log line is ready for the next real dual-side
  pass; no deploy-specific behavior changed.

## 5. What was NOT changed (and why)

- No new retry/backoff inside `SteamTransport`: the host warm-up pump and guest
  handshake already retry, and a cert/rendezvous failure is overwhelmingly an
  external network/Steam state problem.
- No automatic proxy/network workaround: modifying local network config from
  the plugin would be outside CUO's responsibility and would not survive a
  user's proxy choice.
- No protocol version bump: no wire format or message semantics changed.
