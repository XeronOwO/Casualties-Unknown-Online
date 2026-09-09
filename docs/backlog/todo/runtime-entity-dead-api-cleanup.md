# Dead runtime-entity relay API: `BroadcastEntitySpawned` has no caller

- Status: Todo
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
