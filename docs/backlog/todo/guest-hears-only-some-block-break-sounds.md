# Guest hears only some of the host's block hit/break sounds

- Status: Todo
- Priority: Medium-High
- Category: World audio / block damage sync
- Source: User acceptance finding (2026-09-21): while the host mines continuously the guest hears only part of the block hit/break sounds, although every hit plays a sound for the host.
- Related: `review/host-metal-scrap-block-place-sound-not-synced-to-guest.md` (its non-goal states that block hit/break sounds "already replay through the native remote DamageBlock apply path" — this report falsifies that claim, so the one-shot world/item sound family is audited here), `review/sync-player-pain-vocalizations-and-bark.md`, `review/guest-background-ghost-item-ground-sounds.md`

## Evidence

- The guest produces the sound itself when it replays the damage: the host's
  `WorldGenerationDamageBlockPatch` postfix calls `BlockBreakSync.OnBlockDamaged`, which sends
  `BlockDamaged` (host broadcast, guest report); the receiving side calls
  `world.DamageBlock(cell, dmg, true, metalBonus, true)` in `BlockBreakSync.OnRemoteBlockDamaged`,
  and the native sound is played there. A missing sound therefore means a missing or skipped replay.
- Candidates to prove or exclude, in order:
  1. Coalescing or dropping of the damage events — the pending/report bookkeeping and the 1 s
     throttle in `BlockBreakSync`, plus the partial-damage ledger (`BlockReportChannel`,
     `RemoteBlockDamageLedger`) where consecutive hits on one cell may fold into a single message.
  2. Native replay guards — `OnRemoteBlockDamaged` deliberately skips a cell whose `DamageBlock`
     would create a transient damage entry on air (the code comment names it), and an already
     broken cell cannot play the hit sound again.
  3. The host's own report — whether every local hit that played a sound actually produced a
     message (the `DamageBlockOrigin` call scope and the postfix's applied-damage calculation).
  4. Receiving-side suppression — the `SoundPlayPatch` / `SoundCaptureContext` scopes must not
     swallow a remote-replayed world sound.

## Goal and acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Host mines one block repeatedly | Every hit that plays a sound on the host plays the matching sound on the guest |
| 2 | The block breaks | The break sound exactly once, no double audio |
| 3 | Guest mines, host listens | Same, reverse direction |
| 4 | A third peer listens | Same cadence |
| 5 | Fast consecutive hits (the reported cadence) | No coalescing that loses an audible hit |
| 6 | Two players hit the same block | Each hit audible to the other |

## Non-goals

- No continuous audio stream and no per-frame sound messages.
- Not re-designing block damage authority; only the audible outcome and its report cadence.
