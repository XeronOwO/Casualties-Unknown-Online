# Warm-Up Backoff Self-Check (offline-member P2P noise)

The 2026-08-14 log sweep recorded ~40 `k_EResultConnectFailed` warnings per
minute on the host for a SteamId whose session entry was already gone. This
cycle traces the actual sender, fixes the unbounded 1 s retry, and closes the
backlog item. No protocol or world-state change.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Host warm-up pump pings every un-handshaken lobby peer every `HandshakeRetryInterval` (1 s), regardless of session state | SessionService.cs:408-450 |
| 2 | `PacketSender.Send` discarded the transport's send verdict, so the pump could not know a peer is unreachable | PacketSender.cs:24-31 (pre-change) |
| 3 | SteamTransport logs every failed send with session state/end reason — one warning per failed ping | SteamTransport.cs:45-67 |
| 4 | The entity state stream already stops for a removed member: `OnMemberRemoved` drops the entity before the next 20 Hz broadcast can include it | EntitySyncService.cs:371-389 |
| 5 | The other periodic streams iterate `_session.Members` and gate on `SessionActive`; member removal clears presence, so they stop too | ItemService.cs:125-135, EnemySyncService.cs:274-284 |
| 6 | Real-log evidence: host `2026-08-10-24.log.gz` line 732 removes member `76561199526807662` at 15:52:24, then lines 758-776 and 794-802 show 1/s `ConnectFailed` to the same ID at 15:52:45-54 and 15:53:11-15 | real game log (host) |
| 7 | Correlation: sandbox `2026-08-10-33.log.gz` shows the same guest restarting and re-entering the lobby at 15:52:39-42, still un-handshaken; the host's warm-up pump is the sender, not a stale entity stream | real game log (sandbox guest) |

Whole-family audit: the host has exactly one periodic sender that targets
un-handshaken peers — `SendPeerWarmup`. The guest-side `RetryHandshakeIfNeeded`
is deliberately NOT backed off: the sandbox log shows only one transport
failure per restart followed by successful sends that still need the 1 s
retry to complete the handshake (Phase 0 finding: persistent retry is
correct). The entity/enemy/item/fluid periodic senders all target presence
members and already stop on removal.

## 2. Design

- New pure machine `PeerWarmupBackoff` (Runtime/Session): per-SteamId
  exponential backoff 1 s → 2 s → 4 s → … capped at 10 s; one successful send
  resets the peer; `Reset()` clears all state on a lobby change.
- `PacketSender.TrySend` exposes the transport's existing bool verdict without
  changing the `Send` contract used by every other sender.
- `SendPeerWarmup` consults `ShouldSend(peer, nowMs)`, sends via `TrySend`, and
  records success/failure. A broken session now retries ~14 times per minute
  worst-case instead of 60, and a healed session is picked up on the next
  backoff boundary; a healthy peer keeps the exact 1 s cadence.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| warm-up cadence (healthy peer) | unchanged — success resets and the 1 s pump continues | WarmupBackoffSimulationTests.cs (3 pings in 3 s) |
| warm-up cadence (broken peer) | 1 s → 2 s → 4 s → 8 s → 10 s cap | PeerWarmupBackoffTests.cs:31-54 |
| success after failure streak | resets to initial 1 s delay | PeerWarmupBackoffTests.cs:57-69 |
| peer independence | per-SteamId state | PeerWarmupBackoffTests.cs:72-83 |
| lobby change | all failure history cleared | PeerWarmupBackoffTests.cs:86-96; SessionService.cs:553-557 |
| PacketSender contract | `Send` behaviour unchanged, new `TrySend` reports the verdict | PacketSender.cs:20-45 |
| entity state stream on member removal | unchanged — no stale PlayerState to removed members | EntitySyncService.cs:371-389 |
| guest handshake retry | unchanged — 1 s retry is the handshake-establishment path | SessionService.cs:379-395 (not wired to backoff) |

## 4. Verification design

- L0 simulation: 5 pure-machine tests + 2 full-stack fake-network simulations
  (broken link backoff schedule and healthy-link cadence preservation).
- Static evidence: decompiled/log evidence in §1 plus the unchanged-path
  citations above.
- Runtime evidence: development-period rule — L0 simulation + static evidence,
  no manual acceptance (user 2026-08-16 mandate). Post-deploy smoke is limited
  to the deploy.ps1 copy to the real game dir; the next real dual-side pass
  can compare ConnectFailed warning density but is not required now.
