# Dead runtime-entity relay API: `BroadcastEntitySpawned` has no caller

- Status: Review
- Priority: Low
- Category: Runtime / dead code / world entities
- Source: round-4 independent re-review of `review/runtime-entity-spawn-backfill.md` (2026-09-09)
- Related: `review/runtime-entity-spawn-backfill.md`

## Problem (evidence)

`IWorldControl.BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg)` and
its forwarding chain (`WorldChannelRelay.BroadcastEntitySpawned` →
`RuntimeEntityChannel.BroadcastEntitySpawned`) have **no call site**. The live relay
goes through `RuntimeEntityChannel.SendEntitySpawned`'s host branch, which broadcasts
to every member INCLUDING the source — the reporter's echo is exactly what
acknowledges its pending report, and the keypad-code enrichment depends on the source
receiving the relay.

The stale `NetMsg.cs:51` comment that described the relay as "(source excluded)" was
corrected in the same round; the dead method was left in place.

## Goal

Decide with evidence and keep one shape:

- delete the surface from `IWorldControl`, `WorldService`, `WorldChannelRelay` and
  `RuntimeEntityChannel` when nothing needs source-exclusion, or
- wire it to the caller that genuinely needs it (and then prove the reporter's pending
  report is acknowledged by another path).

## Acceptance

- `rg 'BroadcastEntitySpawned' src tests` returns either zero hits (deleted) or exactly
  the call site(s) that need it, each with a comment naming why the source is excluded.
- The live relay still reaches the reporter (its echo is the acknowledgement).
- Full build + tests + `dotnet format` green; no behaviour change on the live path.

## Landed (2026-09-09) — deleted

**Decision: delete.** Nothing needs source-exclusion on this channel, and a
source-excluding relay would be actively wrong: the reporter's echo is the
acknowledgement that stops its pending re-report, and the host's enriched keypad code
rides the relay back to the reporter.

- Removed the interface member (`IWorldControl.cs`), the facade forward
  (`WorldService.cs`), the relay forward (`WorldChannelRelay.cs`) and the implementation
  (`RuntimeEntityChannel.BroadcastEntitySpawned`, which was the only
  `ISessionControl.BroadcastExcept` caller for `NetMsg.EntitySpawned`).
- `ISessionControl.BroadcastExcept` stays: 30+ other relays use it (`BlockPlaced`,
  `BlockDamaged`, `EntityEvent`, `Chat`, `CraftReport`, …) — only this dead forward was
  removed.
- The live relay is untouched: `RuntimeEntityChannel.SendEntitySpawned`'s host branch
  records the accepted creation and `_session.Broadcast(NetMsg.EntitySpawned, msg)`
  reaches every member including the source; the unmaterializable-report branch
  (`ReportEntitySpawnUnmaterialized`) also broadcasts to every member and documents the
  echo as the acknowledgement.

## Verification

- `rg 'BroadcastEntitySpawned' src tests` → **zero hits**.
- Full build 0 warnings / 0 errors; full suite + normative gates green after the
  deletions (they shifted 15 evidence anchors, re-verified and updated in the same
  commit); `dotnet format` clean.
- No behaviour change on the live path: the removed method had no call site, and the
  live path's source-included broadcast is unchanged.
- Deployment: `tools/deploy.ps1 -GameDir "<game-dir>"`; deployed hashes compared against
  the build output (all six CUO assemblies match; see the sibling ticket for the hash list).
