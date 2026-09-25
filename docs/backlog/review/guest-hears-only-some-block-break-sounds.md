# Guest hears only some of the host's block hit/break sounds

- Status: Review
- Priority: Medium-High
- Category: World audio / block damage sync
- Source: User acceptance finding (2026-09-21): while the host mines continuously the guest hears only part of the block hit/break sounds, although every hit plays a sound for the host.
- Related: `review/host-metal-scrap-block-place-sound-not-synced-to-guest.md` (its non-goal stated that block hit/break sounds "already replay through the native remote DamageBlock apply path" — this report falsifies that claim for the BREAK half, and that non-goal is corrected here), `review/sync-player-pain-vocalizations-and-bark.md`, `review/guest-background-ghost-item-ground-sounds.md`, `todo/unhooked-damage-block-callers.md` (the same family's remaining report-coverage gap)

## Root cause (from source, not from the report's narration)

A player's break reaches the other sides as TWO facts of one break:

1. the **air write** — the `SetBlock(0)` inside `WorldGeneration.DamageBlock` runs the
   `WorldGenerationSetBlockPatch` postfix, which reports the mutation immediately
   (`WorldEventSync.OnBlockSet` → `BroadcastBlockPlaced` on the host, `SendBlockPlacedReport` on a
   guest);
2. the **break report** — `BlockBreakSync.OnBlockDamaged` sees the block gone and HOLDS the report one
   frame so the drops' `Item.Start` folds in (`BlockBreakPendingState`, flushed at frame end by
   `GameAdapter`'s pump).

The air write therefore always lands first, so on every receiving side the cell is already air when
the break report arrives, and both receive paths return before the game's damage roll
(`WorldEventSync.OnRemoteBlockPlaced`'s `blockIsAir` guard, `BlockBreakSync.OnRemoteBlockDamaged`'s
"damage only against an already-air cell: there is no block to damage — ignore it"). The native roll
is where the break presentation lives — the broken block's `hitsound` plus a random step clip, the
break particle prefab and `DustBig` (`WorldGeneration.cs:741-750`) — so the side that COMPUTED the
break heard it and every other side saw the block vanish silently. The hit sounds of a surviving block
were never lost (each damage-only report replays through the roll); the breaks were, which is what the
user described as hearing "only part of" them.

## Implementation

- New pure rule `RemoteBreakPresentation` (Runtime): `Route(playerBreak, writtenBlock, cellHeldBlock)`
  decides between a plain write and the game's own break roll, and `LethalDamage(health, currentDamage)`
  is the local lethal remainder the roll needs (the received report's own number is deliberately not
  the input — this side's damage row can be behind the source's).
- `BlockPlacedMsg.PlayerBreak` (a new wire member; `ProtocolVersion.Current` is bumped in the same
  change, whose doc comment carries the per-number log) marks an air write that is the block-removal
  half of a damage roll. It is stamped by `WorldEventSync.OnBlockSet` from the `DamageBlockOrigin`
  call-identity scope (so an earthquake/environment write and a placement carry false), rides the
  relay unchanged, and is STORED with the guest's pending report (`PendingBlockReport`) so a fallback
  re-report — the host's first chance to learn of that write — cannot turn a break back into silence.
- New thin applier `RemoteBlockWrite.Apply` (GameAdapter) is the one place the route becomes calls:
  a claimed write that finds the cell standing runs `WorldGeneration.DamageBlock` with the lethal
  remainder (`ignoreLoot`, under the caller's `RemoteApply` scope), so the sounds, the pitch
  randomization, the mixer group and the break particles all stay the game's; every other write stays
  `SetBlock`. Both receive paths use it (the host branch, the guest branch, and the accepted break whose
  cell still stands).
- `BlockBreakSync.OnRemoteDamageBrokeBlock` (the lost-air-write shape) now applies the break through
  the same applier instead of only recording the state — which also stops the host's world from keeping
  a block its own difference table already called air — and relays it with the claim.
- The once-only guard is the CELL's own state, not extra bookkeeping: a claimed write that finds the
  cell already air is a plain write, so the reporter's own echo, a repeated report and a duplicate air
  write cannot re-present the break.
- Silent siblings stay silent by construction: an earthquake/environment air write
  (`WorldGeneration.Update`'s own `SetBlock`), a placement, a state snapshot row, a correction and the
  late-joiner block-state table carry no claim and stay raw writes.

## Evidence

- `tests/CasualtiesUnknownOnline.Tests/World/RemoteBreakPresentationTests.cs` — the decision table (8
  route rows), the once-only property, and the lethal-damage arithmetic (6 rows + the non-negative
  floor).
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests/BlockBreakPresentationGateTests.cs` — the
  routing pins: the message's member census, every `BlockPlacedMsg` construction site stating its
  claim (with a census floor and a matcher self-test), both receive paths using the shared applier, the
  applier consulting the rule and reaching `DamageBlock` and never calling `Sound.Play`, and the claim
  being read from the damage-roll scope.
- `tests/.../World/PendingBlockReportTableTests.cs` — the claim survives the table's upsert, its cap
  path and its replay.
- `tests/.../World/GuestBlockReportRecoveryTests.cs` — the re-report carries the break's claim (and an
  environment write stays silent) end-to-end over the wire; the existing recovery rows updated.
- Red→green: the routing pins were observed failing on the pre-fix tree (`src/` at HEAD) before the
  implementation — `%TEMP%/cuo-red.txt`.
- The full solution builds and the suite passes (focused → normative gates → full with build); evidence
  run: `%TEMP%/cuo-full-verify.txt`.
- Independent adversarial review (fresh subagent, frozen tree) — `%TEMP%/cuo-review-*.md`.
- Self-check: `docs/evidence/selfchecks/world/guest-hears-only-some-block-break-sounds-selfcheck.md`
  (§4 states the reachable red and the Unity-bound limit; §5 lists what a real session must show).

## Goal and acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Host mines one block repeatedly | Every hit that plays a sound on the host plays the matching sound on the guest |
| 2 | The block breaks | The break sound exactly once, no double audio |
| 3 | Guest mines, host listens | Same, reverse direction |
| 4 | A third peer listens | Same cadence |
| 5 | Fast consecutive hits (the reported cadence) | No coalescing that loses an audible hit |
| 6 | Two players hit the same block | Each hit audible to the other |

## Acceptance status

Code-complete and in `review/` for the final unified acceptance pass. Rows 1-6 are **not** proven by
this cycle's tests: the presentation is a Unity icall (`WorldGeneration.DamageBlock`, `Sound.Play`,
the break prefab) and the test host cannot run it. What the machine checks is the routing the user's
report depended on — the side that must run the game's own break roll does run it exactly once, and the
sides that must stay silent do. The audible result needs the user's two-client session (self-check §5).

Not deployed this cycle (user instruction 2026-09-25: no deployment, the machine is in use for a
session) — the deployed artifact stays at the previous build, and the deployment plus the acceptance
run remain the user's release-cycle actions.

## Non-goals

- No continuous audio stream and no per-frame sound messages (the claim rides an existing message as
  one bool).
- Not re-designing block damage authority; only the audible outcome and its carrier.
- Not re-anchoring `WorldGenerationDamageBlockPatch` to the inner overload — the two unhooked native
  callers (footstep crush, spider burrow) are a report-COVERAGE gap and are ticketed separately:
  `todo/unhooked-damage-block-callers.md`.
