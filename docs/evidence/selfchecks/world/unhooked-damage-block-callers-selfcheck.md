# Self-check — the two native `DamageBlock` callers that were never hooked

- Ticket: `docs/backlog/todo/unhooked-damage-block-callers.md` (2026-09-26 cycle, HEAD started at `52320ca0`)
- Change: re-anchor `WorldGenerationDamageBlockPatch` from the converting `Vector2` overload to the BODY
  overload, widen the report seam from a world position to the cell, gate CUO's custom-tile drops on the
  native `ignoreLoot`, and pin the anchor with a gate.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The BODY overload every native roll enters | `WorldGeneration.cs:711` — `DamageBlock(Vector2Int pos, float dmg, bool hitSound = true, bool bonusMetal = false, bool ignoreLoot = false)`: folds the metallic ×10 (`:715`), accumulates the cell's row (`:720-739`), and at `damage >= health` plays the hit and step sounds, spawns the break particles and the dust, rolls the loot UNLESS `ignoreLoot` (`:751-837`), writes `SetBlock(pos, 0)` (`:839`) and drops the row (`:841`). |
| 2 | The forwarder that was hooked | `WorldGeneration.cs:851-854` — `DamageBlock(Vector2 pos, float dmg, bool hitSound = true, bool bonusMetal = false)` converts with `WorldToBlockPos` and forwards with `ignoreLoot=false`. Nothing else. |
| 3 | The five native call sites | Mining/attack `Body.cs:1929`, `Limb.cs:384`, `TurretScript.cs:144` enter the forwarder; the footstep crush `Body.cs:2709` (three cells per grounded step, only `health == 1f` blocks, `ignoreLoot: false`) and the spider burrow `SpiderHandler.cs:218` (`CheckForBlockDamage`, gated on `burrowWalls`, sets `biteCooldown = biteCoolToSet * 0.3f`, `ignoreLoot: true`) call the body DIRECTLY. |
| 4 | The gap this cycle closes | With the anchor on (2), the two direct callers produced no report, opened no `DamageBlockOrigin` scope (so `WorldEventSync.OnBlockSet` stamped `playerBreak = false` and the peer wrote air silently), and folded no drops. |
| 5 | The report seam | `IPatchBridge.OnBlockDamaged(Vector2 pos, …)` → `GameAdapterBridge` → `BlockBreakSync.OnBlockDamaged`, which immediately converted the position back with `WorldToBlockPos` and sent `cell.x, cell.y` on `BlockDamagedMsg` — the wire already carries the CELL, so the position never left this process. |
| 6 | The remote-apply sites | `RemoteBlockWrite.Apply` (the relayed break, `LethalDamage`, `ignoreLoot: true`) and `BlockBreakSync`'s two applies (the host relay and the guest broadcast) call `world.DamageBlock(cell, dmg, true, metalBonus, true)` inside a `RemoteApply` scope (`BlockBreakSync` enters it around the whole received message). |
| 7 | The scope's readers | `UtilsCreateDropPatch` (marks the roll's `Utils.Create` results as block drops), `WorldEventSync.OnBlockSet`'s air-write claim (`CallContext.Current == DamageBlockOrigin` ⇒ the peers must present the break), `GameAdapterTileContentProvider.TrySpawnDrops` (reached only through the patch's `OnCustomTileBroken`). |
| 8 | The frozen guest enemy copy | `EnemyPatches` prefix-skips `SpiderHandler.Update`, `FixedUpdate`, `OnCollisionEnter2D` and `OnCollisionStay2D` while the spider has a `RemoteEnemyDriver` in its parents, so a guest's clone never reaches `CheckForBlockDamage`. |

## 2. Decisions

1. **Re-anchor to the body overload** (the ticket's clean fix), not a second patch on the body while the
   forwarder stays hooked: the forwarder's calls reach the body too, so two anchors would report the
   mining/attack family twice and need a re-entrancy flag to suppress it. One hook on the choke point
   covers all five call sites and deletes the blind spot instead of moving it.
2. **Widen the seam to the cell.** The patch already holds the cell (it is the body overload's own
   parameter), `BlockBreakSync` converted the position back on entry, and the message carries the cell
   pair — so the cell is what travels, and no position-to-cell formula can drift between the sender's
   report and its own write.
3. **`ignoreLoot` also gates CUO's custom-tile drops.** The game gates its WHOLE loot step on that flag
   (`WorldGeneration.cs:751`); CUO's authored mod drops belong to the same step, so the spider burrow
   (`ignoreLoot: true`) must not spawn them. For the forwarder's callers the flag is always `false`, so
   this is a no-op for every pre-existing path.
4. **The patch skips the bridge for a remote apply** (`__state.IsLocalAction`). The peers already have
   that damage; the deep guard stays in `BlockBreakSync` (`IsRemoteApply`), and the air-write claim is
   additionally protected by `WorldEventSync.OnBlockSet`'s own early return under `RemoteApply`.
5. **Both halves land in one cycle.** The ticket deferred the spider half until "the role semantics of a
   guest's spider clone" were verified: row 8 answers it — the frozen copy cannot run the burrow path,
   so the only new spider reports are the host's own (which is exactly acceptance row 2), and no new
   guest→host enemy-driven traffic appears.
6. **No wire change and no protocol bump.** `BlockDamagedMsg` already carries `X`/`Y`; the seam widening
   is process-internal. Recorded as a fact, not as a merit.

## 3. Whole-family audit

- **All five native call sites**: the three that entered the forwarder keep their exact behaviour (the
  scope now opens one stack frame deeper on the same call), and the two that entered the body are the
  fix.
- **The applier's own rolls** (`RemoteBlockWrite.Apply`, the two `BlockBreakSync` applies): they now pass
  through the patch. They stay silent (decision 4), they cannot roll loot (every one of them passes
  `ignoreLoot: true`, so `Utils.Create` never runs inside the scope) and they cannot spawn custom-tile
  drops (`OnCustomTileBroken` is local-only).
- **The air-write claim**: a footstep crush or a burrow break now stamps `playerBreak = true` (the roll
  opens `DamageBlockOrigin`), which is what makes the peer PRESENT the break through its own native roll
  instead of writing silent air — the presentation half of `done/guest-hears-only-some-block-break-sounds.md`,
  now covering these two callers as well.
- **The character-sound capture**: the two callers' block hit/step sounds now run inside
  `DamageBlockOrigin`, so they cannot be classified as a character sound (the walk-path crush is the case
  that could have been read as a footstep). `SoundPlayPatch`'s scope switch is a whitelist, so nothing
  else changes.
- **The custom-tile drop provider**: gated on `!ignoreLoot` (decision 3); the forwarding callers keep
  `ignoreLoot=false` and therefore keep their authored drops. The crush is the one NEW caller of that
  hook, and it has no executing test (the provider needs the running game). What the static audit can say
  about the duplication risk is structural: one cell is either a vanilla block index (the game's own loot
  switch) or a mod's custom index (the provider), the peer's presentation roll passes `ignoreLoot: true`
  and therefore rolls nothing, and the breaker's drops ride the break message. What it cannot say is
  "observed once" — that is the dual-client run.
- **The evidence matrix**: `docs/evidence/sync-coverage-evidence.json` row W2 quoted the report call
  itself, so the quote moved with the code in the same change; its second quote for this file (the
  `applied` delta) is unchanged.
- **The gate inventory**: `docs/evidence/normative-gates.md` carries the new contract with its four test
  names.

## 4. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `WorldGenerationDamageBlockPatch` anchor | `[typeof(Vector2), typeof(float), typeof(bool), typeof(bool)]` → `[typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool)]`; the Prefix uses the cell parameter (no `WorldToBlockPos`) | `DamageBlockHookCoverageGateTests.ThePatch_AnchorsTheOverloadEveryNativeCallerEnters`; red before, green after |
| The report seam | `OnBlockDamaged` takes `Vector2Int cell` at all three declarations; `BlockBreakSync` drops its conversion | `DamageBlockHookCoverageGateTests.TheReport_CarriesTheCellRatherThanAWorldPosition`; `FullyQualifiedNameGateTests`/build |
| Custom-tile drops | `OnCustomTileBroken` additionally requires `!ignoreLoot` | **static only**: the native gate read plus the §3 audit; the Unity-side provider has no executing test (limit §8), so this row is a claim about the code, not a green case |
| Remote apply | The Postfix returns before the bridge when `!IsLocalAction` | patch source; `BlockBreakSync.IsRemoteApply` remains the deep guard; `WorldEventSync.OnBlockSet` early-returns under `RemoteApply` |
| `applied` (the sender's contribution) | unchanged: the row delta after minus before, `0` on a break | the evidence matrix's second quote for this file is untouched |
| The wire | unchanged, no new member, no version bump | `BlockDamagedMsg` carries `X`/`Y`; `ProtocolNumberGateTests` green |
| The gate itself | Roslyn reader of the `[HarmonyPatch]` declaring type and argument-type list + the cell pin, with positive/negative samples and a floor | `TheAnchorCensus_ReadsTheAttributeArgumentsAndIgnoresMentions`, `TheChokePointPredicate_AcceptsOnlyTheBodyOverload`, `TheAnchor_NamesTheWorldGenerationTargetAndTheBodyOverload` |

## 5. Red, then green

The matcher was frozen BEFORE the red was recorded, and `src/` was untouched at that moment (the
previous cycle's lesson: a red recorded against an earlier matcher cannot be reproduced from the
delivery tree).

- **Red** (frozen gate, unmodified `src/`): 2 failed / 8 passed / 10 — the anchor test named the
  forwarder `DamageBlock(Vector2, float, bool, bool)` and the seam test named `Vector2` in
  `IPatchBridge.cs`; the 8 passing cases are the matcher's own samples, which is what proves the
  predicate rejects the old anchor for the right reason. `%TEMP%/cuo-red-anchor-gate.txt`.
- **Green** (after the change): the focused set of 51 cases across
  `DamageBlockHookCoverageGateTests`, `SyncCoverageGateTests`, `BlockBreakPresentationGateTests` and
  `PatchBridgePortShapeGateTests`. `%TEMP%/cuo-green-anchor-gate.txt`.
- **Family**: the block-break/damage family — 10 gate cases + 109 behaviour cases (net48), 0 failed.
  `%TEMP%/cuo-family-anchor.txt`.
- **Ladder**: `dotnet format` exit 0 with `git status --short` byte-identical before and after
  (`%TEMP%/cuo-format-anchor.txt`, `cuo-status-before-format-anchor.txt`,
  `cuo-status-after-format-anchor.txt`); the full suite WITH build, unfiltered except the mid-cycle
  delivery-checklist gate: 4075 + 244 passed, exit 0 (`%TEMP%/cuo-full-anchor-prereview.txt`), while the
  gate-project run of the same moment totals 245 with that one gate red by design
  (`%TEMP%/cuo-gates-anchor2.txt`). After the review fixes: a second `dotnet format` (exit 0,
  status identical — `%TEMP%/cuo-format2.txt`) and the FINAL unfiltered full suite with build,
  checklist complete: 4075 + 248, exit 0 (`%TEMP%/cuo-full-anchor-final.txt`).

## 6. Verification table

| # | Command | Result | Artifact |
|---|---|---|---|
| 1 | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~DamageBlockHookCoverageGateTests"` (before the change) | 2 failed / 8 passed / 10 | `%TEMP%/cuo-red-anchor-gate.txt` |
| 2 | focused gate set (after the change, before the review fixes) | 51 / 51 | `%TEMP%/cuo-green-anchor-gate.txt` |
| 2b | `--filter "FullyQualifiedName~DamageBlockHookCoverageGateTests"` (after the review fixes) | 13 / 13 | this row's run |
| 3 | block-break/damage family | 10 gate + 109 behaviour, 0 failed | `%TEMP%/cuo-family-anchor.txt` |
| 4 | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/…csproj` (mid-cycle) | 245 total: 244 passed + 1 failed — the reset delivery checklist, by design | `%TEMP%/cuo-gates-anchor2.txt` |
| 5 | `dotnet format CasualtiesUnknownOnline.slnx` (twice: before and after the review fixes) | exit 0, `git status --short` identical before/after both times | `%TEMP%/cuo-format-anchor.txt`, `%TEMP%/cuo-format2.txt` |
| 6 | full suite with build, `--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` | 4075 + 244 passed, 0 failed, exit 0 | `%TEMP%/cuo-full-anchor-prereview.txt` |
| 7 | Independent adversarial review | see §7 | `%TEMP%/cuo-review-unhooked-damage-block-callers.md` |
| 8 | FINAL unfiltered full suite with build, checklist complete (the gate project now runs its checklist case too) | 4075 + 248, exit 0 | `%TEMP%/cuo-full-anchor-final.txt` |

## 7. Review disposition

Independent adversarial review, fresh context, frozen working tree
(`%TEMP%/cuo-review-unhooked-damage-block-callers.md`, 304 lines). It re-ran the new gate (10/10) and the
delivery-checklist gate (1/1) on the frozen tree and confirmed `git status --short` was byte-identical
before and after. Verdict: no BLOCKER — anchor coverage, the no-echo property, the frozen-guest-clone
answer, the `ignoreLoot` reading and the removed position conversion were all verified, and it found no
claim anywhere that a heard result had been proven.

| Finding | Severity | Disposition |
|---|---|---|
| Acceptance row 2 promised that the guest applies the wall damage and its crack converges; the receiver discards a damage report for a cell that is already air (`BlockBreakSync`'s `blockIsAir` guard) | MAJOR | Fixed in this commit. Row 2 now states the two outcomes separately: a wall the burrow only DAMAGES converges through the report, a wall it BREAKS is presented through the air write's claim and its damage value is discarded by design. §8 records the discard point. The reviewer read the partial-hit case as losing the apply too; the code does apply it (`world.DamageBlock(cell, dmg, true, metalBonus, true)` runs whenever this side still holds the block), so the row says exactly that instead of deleting the claim. |
| The crush's new custom-tile drop path has never executed and has no test, while §4 cited an audit as coverage | MAJOR | Fixed: §4 marks the row static-only, §3 states what the audit can and cannot say (one cell is either a vanilla index or a mod index, the peer's roll is `ignoreLoot: true`, the drops ride the break message), and the ticket names the dual-client observation. No pure helper was extracted: the decision is the one flag the native gate already owns, and a helper would still not execute the Unity-side provider. |
| Two documented numbers were the FILTERED count (244) while the cited run totalled 245 | MAJOR | Fixed: §6 row 4 states 245 (244 passed + the by-design checklist gate) against its artifact, and the checklist cites the final unfiltered run. |
| The gate pinned the argument types but never the declaring type | MINOR | Fixed: the anchor test asserts `WorldGeneration`, and `TheAnchor_NamesTheWorldGenerationTargetAndTheBodyOverload` adds that axis with a same-shaped `Sound.DamageBlock` sample. |
| The gate header and the previous cycle's selfcheck still cited the ticket's `todo/` path | MINOR | Fixed: the header says `review/`, and the older selfcheck's row carries the one-clause "since closed" note. |
| "deletes the blind spot rather than moves it" / "fails instead of silently dropping" overstated the gate's reach | MINOR | Fixed in the ticket and in the new `normative-gates.md` row: the anchor closes the blind spot and a source-read gate pins it, while attribute stacking and manual `PatchProcessor` anchors are outside that gate's reach. |
| Audit snapshots under the gitignored `reversing/sync-audit/**` keep the pre-change quote | NIT | No action: gitignored, dated audit output. |

## 8. Limits

- **The gate pins the anchor, not the audible result.** That the footstep crush and the spider burrow
  now ENTER the hooked roll, open the damage scope and stamp the air write as a player break is proven
  mechanically; that the guest HEARS the break, sees the crack converge and that the drops land is the
  user's dual-client acceptance run.
- **The gate reads source.** An anchor expressed another way (a stack of `HarmonyPatch` attributes, a
  manual `PatchProcessor` call) is out of its reach; the counts it asserts make a silent disappearance
  fail loudly instead of passing by checking nothing.
- **The clone answer is static.** Rows 8 rests on the patches, not on a running guest: an enemy that has
  not yet been bound by a `RemoteEnemyDriver` on a guest would run natively and, with this change, report
  its roll to the host — the report is arbitrated like any other guest mutation, and that unbound window
  is pre-existing and unchanged by this cycle.
- **Report volume is a property of the call sites, not of a limiter.** The footstep path damages up to
  three cells per grounded step and the burrow path is gated by the native `biteCooldown`; the report
  follows the roll one-for-one and adds no cadence of its own.
- **`ignoreLoot` on the custom-tile path has no Unity-side test**: the provider needs the running game,
  so that rule is covered by reading the native gate and by the audit in §3, not by an executing case.
  The risk it leaves open is a duplicated authored drop set; §3 states why the structure rules that out,
  and the dual-client run is what observes it.
- **A damage report for a cell that is already air is discarded, by design.** A break's air write is sent
  synchronously inside the roll while its damage-carrying report waits one frame for the drops, so on the
  receiving side the cell is air before the value arrives and `BlockBreakSync`'s `blockIsAir` guard drops
  it — `DamageBlock` on air would invent a transient row and play a hit sound for a block that is gone. A
  wall the roll only DAMAGES is the other case: nothing was written air, so the report IS applied and the
  crack state follows before the next snapshot.
