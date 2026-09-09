# Guest world-block mutations have no periodic re-report

- Status: Review
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

## Landed (2026-09-09)

**Mechanism (design option 1, adapted).** The recovery unit is the guest's
*unacknowledged report*, not a deviation-vs-baseline table: the guest keeps
`PendingBlockReportTable` (cell → block, cap 65 536, upsert on a newer local
write), populated by `SendBlockPlacedReport` **before** the send.
`BlockReportFallback` (armed by the first outstanding entry) re-reports every
entry once per 60 s through the new `BlockReportFallbackPump`; a swallowed
report is re-sent until the host answers. An entry is dropped the moment the
host's answer for that cell arrives (the accepted relay echo or the targeted
correction); the whole table is cleared when a new world/layer baseline is
applied (`WorldParamsService.Apply` → `ResetPendingBlockReports`, symmetric to
the host's `ResetDamagedBlocks` at its generation boundary).

**Host side.** The host now answers every report:
- accepted → the relay includes the reporter (same-value `SetBlock` is a no-op;
  the echo is the acknowledgement) and every other member;
- refused (first-writer-wins) → a targeted `SendBlockPlacedCorrection` with the
  host's current cell, so the reporter converges instead of staying diverged
  forever when the host's cell is at the generated baseline (the old silent
  refusal had no heal path at all);
- a remote `BlockDamaged` that breaks the host's block records the air
  transition in the host's difference table and relays it
  (`OnRemoteDamageBrokeBlock`): the `SetBlock(0)` inside `DamageBlock` runs under
  `RemoteApply`, so the local-report hook stays silent and the host's absolute
  snapshot would otherwise omit the cell forever.

**Independent adversarial review (fresh context) → two MAJOR findings fixed.**

1. The world-entry snapshot apply (`OnRemoteBlockState`) did **not** run under
   `CallContext.RemoteApply`, so every snapshot cell fired the SetBlock postfix
   and was echoed back to the host as a guest mutation — the host's own state
   re-reported, and now amplified into a correction per cell per snapshot. The
   apply loop is wrapped in `RemoteApply`, and the air-write bookkeeping is
   skipped for unchanged cells (a reporter's own echo can no longer suppress its
   local building-drop roll).
2. The `WorldSnapshotComplete` marker was the wrong reset boundary: it fires on
   every world-entry/reconnect, so a reconnect-while-in-world would silently
   drop the guest's unanswered (and still locally applied) mutations, while a
   layer regeneration was only covered once the marker arrived. The reset moved
   to the guest's world/layer apply boundary; the marker deliberately does not
   clear.

Minor findings were also fixed: the overflow-log episode flag resets, the
break-path relay includes the reporter (its echo clears the pending entry), the
cadence doc states the real arming semantics, the fallback re-arms on a
backwards `Environment.TickCount` instead of stalling, and the
`BlockPlacedMsg`/`BlockPlacedHandler` comments match the new answer semantics.

**Known limitations (recorded, not hidden).**

- Two local writes to the SAME cell inside one round trip: the first answer
  clears the cell's pending entry, so a swallowed second report falls back to
  the host's 60 s snapshot (the host's authority wins; bounded and
  self-healing). Fixing it needs per-report sequencing on the wire.
- The adapter's arbitration shell (the host answer branch, the break-path
  recording, the snapshot `RemoteApply` wrap) is not exercisable in the test
  host — it needs the live Unity world. Those branches rest on code review plus
  the unified dual-client acceptance pass.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row W1 → `OK` (46 OK /
9 event-only / 0 fallback-only / 9 transient), inline anchors re-anchored and
extended (750 entries), W2 split into
`todo/guest-partial-block-damage-re-report.md`, the break-drop loss recorded in
`todo/guest-break-drops-recovery.md`.

**Verification.**

- Red (recorded in-session): the integration tests failed on the unfixed code —
  `Assert.Single() Failure: The collection was empty` (the report was never
  recorded); after the recording-only step they failed with
  `The collection contained 2 items` (the entry was never cleared). To
  re-observe: comment out `RecordPendingBlockReport(x, y, block);` in
  `SendBlockPlacedReport` (first red), then restore it and remove the `Remove`
  call in `OnBlockPlacedReceived` (second red).
- Green: `GuestBlockReportRecoveryTests` (6) + `PendingBlockReportTableTests`
  (5); full suite 2 612 + 32 gates (2 644) pass after the review fixes;
  `dotnet format` clean; build 0 warnings / 0 errors.
- Deployment (re-verified after the review fixes): the latest build is deployed
  to the machine game dir and every deployed CUO assembly hash equals its build
  output — `CasualtiesUnknownOnline.Runtime.dll` `2D33960C…`, `GameAdapter`
  `7A7C36B4…`, `GameState` `E7AD7F07…`, `Protocol` `B8D3E100…`, `Abstractions`
  `9B46422D…`, `CasualtiesUnknownOnline.dll` `F9A650A8…`.

### Acceptance matrix coverage

| # | Covered by |
|---|---|
| 1 | `SwallowedGuestReport_IsReReportedOnTheFallbackCycleAndConverges` (air write onto a solid host block; the host adopts it, relays, the reporter's echo clears the entry) |
| 2 | the same report/arbitration shape (the host answer is value-agnostic); no separate placement test — the placement branch is the same code path |
| 3 | the same test: the air write IS the recovery entry; the drops arbitration is NOT covered here (its own ticket `todo/guest-break-drops-recovery.md`) |
| 4 | `RefusedReport_IsAnsweredWithTheHostsAuthoritativeValue` (first-writer-wins: the host's cell stands, the reporter converges, no ping-pong) |
| 5 | the same test asserts `w.ReceivedCount(w.G2, NetMsg.BlockPlaced) >= 1` — the accepted relay reaches the third member |
| 6 | `WorldSnapshotComplete_DoesNotDropUnansweredReports` (a reconnect keeps the pending report; the marker is not a world boundary) |
| 7 | `ResetPendingBlockReports_ClearsThePreviousWorldsReports` (the layer/world apply boundary clears); the adapter wiring (`WorldParamsService.Apply`) is code-reviewed only |
| 8 | `PendingBlockReportTableTests.Report_AtCap_RefusesNewCellsButStillUpdatesExisting` + the once-per-episode warning in `RecordPendingBlockReport`; `DefaultCap_MatchesTheHostDeviationTableBound` pins both tables to one bound |
| 9 | the world-entry fan-out (existing `WorldEntrySnapshotTests`); the guest table starts empty per world and the apply-boundary reset is tested |
| 10 | the break's air write is the re-reported entry and the host's applied air write clears the crack via `OnBlockAirWrite` (adapter-side, dual-client acceptance) |
