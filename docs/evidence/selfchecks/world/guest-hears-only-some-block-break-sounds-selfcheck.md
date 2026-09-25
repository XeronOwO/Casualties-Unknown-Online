# Self-check — the guest hears the host's block hit/break sounds (`guest-hears-only-some-block-break-sounds`)

Ticket: `docs/backlog/review/guest-hears-only-some-block-break-sounds.md` (Medium-High).
Cycle: 2026-09-25. Wire change: `BlockPlacedMsg.PlayerBreak` (+ `ProtocolVersion.Current`, bumped in
the same change — its doc comment in `ProtocolVersion.cs` is the per-number log).

## 1. Mechanism inventory (every claim from source)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | A native damage roll plays the presentation: on a break, the block's `hitsound` + a random step clip, the break particle prefab and `DustBig`; on a surviving hit, the `hitsound` only | `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:741-750` (break) and `:844-847` (hit) |
| 2 | CUO reports every LOCAL damage roll through the outer overload's postfix and replays a received report with the native roll | `WorldGenerationDamageBlockPatch` (the `Vector2` overload, `OnBlockDamaged(pos, dmg, bonusMetal, applied)`); `BlockBreakSync.OnRemoteBlockDamaged` → `world.DamageBlock(cell, dmg, true, metalBonus, true)` |
| 3 | A local break produces TWO facts: the air write, sent immediately from the `SetBlock` postfix, and the break report, held one frame for the drops | `WorldEventSync.OnBlockSet` → `BroadcastBlockPlaced` / `SendBlockPlacedReport`; `BlockBreakPendingState.EnterBreak` + `GameAdapter.cs`'s frame-end `FlushPendingBlockBreak` |
| 4 | The air write is therefore always applied first on every receiving side, and the break report's own native roll never runs there | `WorldEventSync.OnRemoteBlockPlaced` and `BlockBreakSync.OnRemoteBlockDamaged` both return before `DamageBlock` when the cell is already air (`blockIsAir` / `changed`) |
| 5 | Consequence: the side that computes a break hears it, every other side sees the block vanish silently — the reported symptom | rows 1-4; the user report is the source |
| 6 | The earthquake's own air write is SILENT where it runs, so it must stay silent everywhere | `WorldGeneration.cs:895` (`this.SetBlock(…, 0)` inside `WorldGeneration.Update`) |
| 7 | The four air-write producers and the correction: local report, host relay, host relay of a guest write, guest fallback re-report, host correction | `BlockReportChannel.SendBlockPlacedReport` / `BroadcastBlockPlaced` / `SendBlockPlacedCorrection`, `GuestReportRecovery.ResendBlocks` |
| 8 | Two native `DamageBlock` callers enter the INNER overload directly and are not hooked (footstep crush, spider burrow) — a report-COVERAGE gap of the same family | `Body.cs:2709`, `SpiderHandler.cs:218`; ticketed as `docs/backlog/todo/unhooked-damage-block-callers.md` (not fixed this cycle — see §2) |

## 2. Whole-family audit

- **Every carrier of the break fact was walked**: the live report, the 60 s re-report (`ResendBlocks`),
  the host relay, the relay of a relayed guest write, the correction, the state snapshot and the
  late-joiner block-state table. The claim rides the live report, the re-report (stored with the
  pending entry, never re-derived from `block == 0` — a guest's own quake break is an air write too)
  and the relay; the correction and the snapshot deliberately carry none (they are state, not a break
  someone watched happen).
- **The silent siblings were named rather than papered over**: an earthquake/environment air write and
  a placement stay raw writes on every side, so no side gains a sound its source never played.
- **The lost-air-write shape** (an accepted break whose cell still stands on the host) was applying a
  bare `SetBlock(0)` through `OnRemoteDamageBrokeBlock`, which also left the host's own block standing
  while its difference table said air. It now goes through the same applier, so the host presents the
  break and its world loses the block.
- **Out of scope, ticketed**: the two unhooked native callers (row 8). The clean fix is re-anchoring
  the patch to the `Vector2Int` choke point, but that also starts reporting an ENEMY-caused roll
  (the spider burrow), whose guest-clone role semantics have to be verified first — a state-sync
  change, not a sound one.

## 3. Self-check table (mechanism × change × evidence)

| # | Rule | Where | Pinned by |
|---|---|---|---|
| 1 | A claimed air write that finds the cell standing is applied through the game's own roll | `RemoteBreakPresentation.Route` | `RemoteBreakPresentationTests.Route_PresentsExactlyTheUnappliedBreak` (8 rows) |
| 2 | A claimed write that finds the cell already air is a plain write — the once-only guard | same | `Route_PresentsABreakOnce_EvenWhenItsWriteIsRepeated` |
| 3 | The roll's damage is the local lethal remainder, never the received number | `RemoteBreakPresentation.LethalDamage` | `LethalDamage_IsTheRemainderOfTheGamesOwnRow` (6 rows) + `LethalDamage_IsNeverNegative` |
| 4 | Every `BlockPlacedMsg` build site states its claim | the four producers | `BlockBreakPresentationGateTests.EveryBlockPlacedConstructionSite_StampsThePresentationClaim` |
| 5 | Both receive paths apply through the shared applier; the applier consults the rule and reaches `DamageBlock`; the presentation is never re-implemented from clip names | `WorldEventSync`, `BlockBreakSync`, `RemoteBlockWrite` | `TheReceivePaths_HandEveryAppliedWriteToTheSharedApplier`, `TheApplier_ConsultsThePureRuleAndRunsTheGamesOwnRoll` |
| 6 | The claim is read from the damage-roll scope, not from a side's role | `WorldEventSync.OnBlockSet` | `TheAirWritesClaim_IsReadFromTheDamageRollScope` |
| 7 | The wire member is a reviewed surface | `BlockPlacedMsg` | `BlockPlacedMessage_DeclaresExactlyThePinnedMembers` |
| 8 | The fallback re-report carries the write's own claim | `PendingBlockReport`, `GuestReportRecovery.ResendBlocks` | `PendingBlockReportTableTests.Report_KeepsABreaksClaim_SoARecoveryReReportCannotSilenceIt`, `GuestBlockReportRecoveryTests.ReReport_CarriesTheWritesOwnBreakClaim` |
| 9 | The forwarded claim reaches the event unchanged | `BlockPlacedHandler` → `BlockReportChannel.FireBlockPlacedReceived` | `GuestBlockReportRecoveryTests` (the host executor double reads the flag) |
| 10 | One re-report frame is 7 bytes with the claim on the wire | `PacketSender` frame | `GuestBlockReportRecoveryTests.SwallowedReportInsideTheWindow…` + `docs/evidence/sync-cadence-measurements.md` |
| 11 | Both outcomes of a CLAIMED write are observable — "presented here" vs "found the cell already air" | `RemoteBlockWrite.Apply` | the two Debug lines (unclaimed writes stay unlogged: they are the high-frequency placements and environment writes) |

## 4. Verification design, and its honest boundary

- **Reachable red**: the routing pins in `BlockBreakPresentationGateTests` were written first and
  observed FAILING on the pre-fix tree (`src/` restored to HEAD, the pure machine absent) — see
  `%TEMP%/cuo-red.txt`. They pin the routing surface the defect lives on (the claim's producers, the
  apply paths, the applier's native roll), not a patch parameter list.
- **Behavioural core**: `RemoteBreakPresentationTests` locks every row of the decision table,
  including the once-only property and the lethal-damage arithmetic.
- **NOT reachable in this host**: the audible result. `WorldGeneration.DamageBlock`, `Sound.Play` and
  the break prefab are Unity icalls; the test host cannot run them, and the production receive path
  reads `WorldGeneration.world` directly. No test in this cycle proves that a sound was heard — it
  proves that the side which should run the game's own roll does run it exactly once, and that the
  sides which should stay silent do.
- **Environment**: `dotnet build` + `dotnet test` (focused → normative gates → full suite with build)
  + `dotnet format`.

## 5. What only a real two-client session can confirm

1. Host mines a block repeatedly → the guest hears the hit sounds AND the break sound, matching the
   host's cadence (the reported defect: the break was silent).
2. The break is heard exactly ONCE per side (no double audio in the frame the drops arrive).
3. Guest mines → the host hears the break (the reverse direction, previously silent on the host).
4. A third peer hears the same cadence.
5. An earthquake's breaks stay SILENT on the guest (no invented sound) — the quake itself rumbles.
6. Blocks that break while the two sides are briefly diverged (a swallowed report) still sound once on
   the side that applies them late.

## 6. Limits recorded for the next reader

- The claim is a BOOL on the air write; a future damage-driven air write that is not a break (none
  exists today) would need its own classification before it can ride the same member.
- A break whose air write AND break report are both lost stays silent on the receiving side until the 60 s absolute block-state snapshot heals the cell. That is a PRE-EXISTING host→guest gap of this family (the host's relays are not tracked as pending; the snapshot carries state, never drops), not a regression of this change.
- The `DamageBlockOrigin` scope is what makes a local write "damage driven"; a roll that a future code
  path runs OUTSIDE that scope would be reported as an environment write and would stay silent.
- The two unhooked native callers (row 8) mean the acceptance matrix' "every hit that plays a sound on
  the host plays on the guest" still has two known exceptions, both ticketed.
