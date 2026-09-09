# Session control convergence: lost readiness/control messages have no re-report

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / session control
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` rows R3, R4, W7 marker finding)
- Related: `todo/guest-block-mutation-re-report.md` (same "swallowed event" family)

## Problem (evidence)

The session/world-entry control path is reliable at the transport but has no
re-report when a message is swallowed while both peers stay connected.

1. **HandshakeAckAck loss is not healed.** The guest retries the handshake at
   1 Hz (`src/CasualtiesUnknownOnline.Runtime/Session/SessionPeerMaintenance.cs:34`,
   `:73`) but only while the session is inactive
   (`src/CasualtiesUnknownOnline.Runtime/Session/SessionService.cs:281`,
   `if (!SessionActive)`); the guest flips `SessionActive` when the ack arrives
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeAckHandler.cs:39`).
   The host only marks the member on the third leg
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeAckAckHandler.cs:43`,
   `member.Handshaken = true;`). A lost `HandshakeAckAck` therefore leaves the
   host's member unconfirmed for the rest of the connection (start gate and
   `WorldReady` fan-out exclude it) while the guest believes it is connected.
2. **SceneState / PlayerJoin have no re-report.** `SceneState` is emitted only at
   the local InWorld/InMenu edge
   (`src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs:358`), and
   `PlayerJoin` once per member sync start
   (`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EntitySyncService.cs:513`,
   `:528`). A swallowed `SceneState` means the host never marks the member
   InWorld, never fires the world-entry fan-out
   (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/SceneStateHandler.cs:59`)
   and the entity stream never starts
   (`src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/PlayerStreamExchange.cs:103`);
   the guest's only escape is the 60 s give-up back to the menu
   (`src/CasualtiesUnknownOnline.GameAdapter/Run/StartGateCoordinator.cs:187`).
3. **`WorldSnapshotComplete` is inert.** The marker is sent last in the ordered
   world-entry group (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs:44`)
   and the receiver fires `WorldSnapshotCompleteReceived`
   (`src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStateMessageService.cs:70`),
   but no production code subscribes to it, so a partial backfill is still not
   distinguishable in behavior. Either a consumer (readiness gate) or removal.

## Goal

A lost control/readiness message converges while the peer stays connected:
the host's member state, the guest's in-world state and the world-entry group
completion all reach a consistent state without requiring a leave/re-enter.

## Design direction (decide at implementation)

1. **Re-ack unconfirmed members** — the host's existing 1 Hz warm-up pump
   (`SessionPeerMaintenance.SendPeerWarmup`, `:78-121`) already targets
   un-handshaken lobby peers; also re-send `HandshakeAck` to peers that have a
   presence record but `Handshaken == false`. The guest already re-sends
   `HandshakeAckAck` on every received ack
   (`HandshakeAckHandler.cs:56`), so the loop closes in 1 s.
2. **Scene-state reconciliation** — a low-frequency (5–10 s) absolute scene-state
   report (or a host request) so a lost InWorld edge heals without the 60 s
   timeout.
3. **Marker decision** — wire `WorldSnapshotCompleteReceived` to the readiness
   gate / late-join completion, or delete the marker and the message id; do not
   leave an inert marker documented as a safety mechanism.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | `HandshakeAckAck` dropped | Host re-acks; member becomes Handshaken within ~1 s; start gate includes it |
| 2 | `HandshakeAck` dropped | Existing 1 Hz guest retry still heals it (regression) |
| 3 | `SceneState` (InWorld) dropped | Host converges without the 60 s timeout; world-entry fan-out fires once |
| 4 | `PlayerJoin` dropped | Roster converges; no duplicate join |
| 5 | `WorldReady` dropped | Guest converges; no double start-gate release |
| 6 | Third-party member | Unaffected; no duplicate fan-out |
| 7 | Reconnect while in world | Same behavior as today (existing handshake fan-out) |
| 8 | `WorldSnapshotComplete` | Either consumed by a readiness gate or removed everywhere (no inert wire id) |

## Non-goals

- Host migration, reconnect semantics changes.
- The kernel checkpoint/journal recovery (already covered by rows K2/K3).
