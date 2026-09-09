# Guest world-block mutations have no periodic re-report

- Status: Todo
- Priority: High
- Category: Network / sync coverage / world blocks
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` rows W1/W2); user-reported suspicion (2026-09-07) — "我印象中世界中的方块没做定时兜底同步"
- Related: `review/block-break-first-writer-wins.md`, `review/trap-destruction-drop-quantity-desync.md`, `todo/sync-cadence-review.md`

## Problem (evidence)

Host → guest converges; guest → host does not.

- The host owns the block difference table and the 60 s absolute resend:
  `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStateMessageService.cs:35`
  (`private readonly Dictionary<(int, int), ushort> _damagedBlocks = [];`),
  `:299` (`public void SendBlockStateSnapshot(ulong targetSteamId)`),
  driven by `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:122`
  (`if (IsHostMode && _session.SessionActive && Time.unscaledTime - _lastSnapshotResend > 60f)`).
- The guest reports a local mutation only at the `SetBlock` trigger and keeps no
  difference table:
  `src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:214`
  (`_world.SendBlockPlacedReport(pos.x, pos.y, block);`),
  `:173` (`// world and applies the table). Guests do not track — they only apply.`).
- The guest's report path is a single send with no retry:
  `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStateMessageService.cs:202`
  (`_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,`).
- A swallowed guest report therefore never enters the host table; the 60 s
  snapshot omits that cell and cannot heal it. The divergence persists until a
  reconnect regenerates the guest world (losing the local mutation and its drops)
  or a later host write lands on that cell.
- Partial block damage (row W2) shares the same asymmetry: the registry is
  host-only (`src/CasualtiesUnknownOnline.Runtime/Session/World/BlockDamageRegistry.cs:32`,
  `if (_session.Role != SessionRole.Host)`), so a swallowed guest damage report is
  not re-reported either; only the terminal break state (this ticket) can heal it.
- The 60 s host resend is not the only exposure: the documented lazy-Steam-P2P
  swallow window is up to ~30 s after world entry
  (`src/CasualtiesUnknownOnline.GameAdapter/World/WorldEventSync.cs:106`).

## Goal

A guest block mutation (place / break / air write) whose report is swallowed
converges to the host's authoritative result without a reconnect, and the host's
absolute snapshot then heals every peer. First-writer-wins semantics stay intact.

## Design direction (decide at implementation)

1. **Guest diff re-report (symmetric to the host)** — the guest keeps a local
   deviation table against its generated baseline and re-reports it absolutely
   (same cadence family as the host, e.g. 60 s) and on member (re)entry. The host
   applies the existing arbitration (`WorldEventSync.OnRemoteBlockPlaced`), so
   first-writer-wins is unchanged.
2. **Host request/repair** — the host asks each in-world guest for its deviation
   table periodically; the guest answers with the absolute set.
3. **Accepted loss** — rejected: this is the user-visible divergence the audit
   was asked to resolve.

Option 1 mirrors the existing host mechanism and should not need a new wire
message (reuse the `BlockPlaced` / `BlockDamaged` family). The re-report must be
idempotent and must never resurrect a cell the host has since overwritten.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest mines a block; its report is dropped by the fake network | Host converges to the guest's cell without reconnect; guest keeps its result |
| 2 | Guest places a block; report dropped | Same |
| 3 | Guest air write (earthquake / environment break); report dropped | Same, including the drop arbitration |
| 4 | Host writes the same cell after the guest's swallowed report | First-writer-wins result preserved; no ping-pong |
| 5 | Third-party guest view | Every peer converges to the same cell |
| 6 | Guest reconnect | Host table and guest world agree; mined blocks are not resurrected |
| 7 | Layer regeneration | Table resets with the new baseline; no stale re-report |
| 8 | Table cap (`MaxDamagedBlocks` = 65536) | Re-report respects the cap; overflow is logged, never silent |
| 9 | Solo → lobby → guest joins | The accumulated diff is handed over exactly once |
| 10 | Partial damage then break, both reports dropped | The terminal state converges; the crack state is healed with it |

## Non-goals

- Anti-cheat / strict validation of guest block reports.
- New wire message ids unless the implementation proves one is required.
- Host-side 60 s cadence tuning (tracked in `todo/sync-cadence-review.md`).
