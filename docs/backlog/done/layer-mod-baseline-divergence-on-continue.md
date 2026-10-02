# A Continue after a run opens a stale world (the layer-mod baseline then diverges)

- Status: Done
- Acceptance (20261002-l): the run's first committed cut moved the pointer to the run's own world, the
  Continue restored that world's mid-run layer-0 cut, and all three clients generated from the same
  decision entry state (`77B6B8…`) and applied the same modifier (index 5) with zero
  `[LayerMod] baseline divergence` — record
  `docs/evidence/acceptance/layer-mod-baseline-divergence-on-continue-20261002-l.md`
- Priority: High
- Category: Persistence / save continue target (observed as a world-generation / layer-modifier divergence)
- Source: agent acceptance batch `20261002-k` (2026-10-02) — observed while staging the late-join/reconnect
  rows of that batch; root cause attributed 2026-10-02 from the run's own artifacts and the local CUO
  repository
- Related: `docs/evidence/acceptance/enemy-snapshot-binding-recovery-20261002-k.md`,
  `docs/evidence/acceptance/world-determinism-world-fingerprint-20261002-k.md`,
  `done/reenter-baseline-adoption.md` (the follow-up: a member that never left the session still generates
  before the host's restored baseline arrives), `review/save-system-mid-run-and-layer-end.md`

## Symptom (evidence)

After the host's leave-world + Continue, both members repeated, every 10 s:

```text
[WRN] [LayerMod] baseline divergence — local segment start 5200E7D148E10BB7426A68F7E2407117 vs host's
      E76DFACE27BBFC1FC9FFB1C2648822EB (world effects may diverge).
```

The host's enemy generation set stayed at 74 animals (69 shadecrawler + 5 trader) while both members held
85 (78 + 7) — the same set the entry census read — so `EnemySnapshot`'s all-or-nothing pairing failed on
every repair cycle (`generation spawn pairing failed (74 host vs 85 guest generated enemies)` +
`snapshot applied: -11 generated bound, 0 runtime spawns, mapping=False`). Observed with artifact
`0.1.0+393d79a8c0d8f4a20320b667f6122884daa656ae` (commit `393d79a8`); artifacts
`k-F-baseline-guest.log`, `k-F-baseline-alt.log`, `k-F-snapshot-latest-*.log`, `k-census-F-*.json` in the
local acceptance artifact directory.

## Root cause (attributed 2026-10-02)

The Continue restored a **different world** than the one the run was playing, because the repository's
Continue pointer had never moved to the run's world:

| Fact | Value |
|---|---|
| The run's world (played, cut twice) | `w-20261002-62b8` — live cut `mid-run` / `menu-return`, `layerIndex 0`, `run.randomState` `30BFAA8A73C067F16911C09DC4498BC7` |
| The world the Continue actually opened | `w-20261001-6986` — live cut `layer-end` / `layer-advance` (2026-10-01), `layerIndex 1`, `biomeDepth 1`, `totalTraveled 307`, `run.randomState` `F8A3757E40C4B5DD365E347EF5B36E33` |
| `saves/index.json` `lastOpenedWorldId` at the click | `w-20261001-6986` |

The host applied `F8A3757E…` at the Continue click and again at its generation boundary
(`k-F-world-host.log`), while both members regenerated from `30BFAA…` — byte-for-byte the run's own
`run.json` baseline (`k-F-baseline-guest.log`: the generation stream was reset to it, and the local replay
read `depth=0`). `WorldSaveService.TryBeginRun`'s own comment, the S2 record
(`review/save-layer-end-save-and-restore.md`: "the picker pointer moves on the first cut") and the test
`TryBeginRun_CreatesAWorldAndPointsTheIndexAtIt` all claimed the pointer moved on the world's first cut;
**no code ever wrote it there** — only a restore or an explicit Worlds-page selection moved it, so a run's
own world never became the Continue target. That old test could not catch it: with one continuable world,
`ContinueWorldId`'s "first world with a snapshot" fallback answered the same value it would have answered
if the pointer had moved.

Impact: the host regenerated the foreign world's layer 1 while the members regenerated the session's
layer 0 — the divergence in the symptom above. The layer-modifier warning is the diagnostic, not a
layer-modifier defect. The 74-vs-85 split is the two worlds' populations, not live deaths: 85 is what the
entry census read on all three clients before the Continue.

## Answers to the questions this ticket opened with

- **Is a member supposed to replay the host's layer-modifier state, or is divergence expected here?** A
  member generates the host's world from the same run baseline; after that its local replay matches by
  construction (batch `20261002-k`'s entry passed that way). The warning is a genuine "the two sides
  generated different baselines" detector, not an expected state.
- **Does the restored cut carry the layer-modifier segment; why does the member not adopt it?** The cut
  carries the run baseline (the generation RNG state), not a per-segment modifier state; the member never
  adopted it because it had already generated from its own last baseline before the restored state
  arrived — and in this run the host had restored a different world entirely.
- **74 vs 85: divergent modifiers or live deaths?** The two different worlds (above), matched against the
  entry census.

## Fix (landed 2026-10-02)

`WorldSaveService.OnCutReported` now moves `lastOpenedWorldId` to the world the service is writing on its
**first committed cut** — exactly the contract the comment claimed — so the world a run plays becomes the
world the native Continue opens. The rule: once per world; never at `TryBeginRun` (an aborted start must
not point at a folder with no snapshot); never for a refused/deferred attempt; a picker selection made
after that first cut still stands; a pointer write that fails is retried by the next committed cut; a
restore keeps its own selection. No wire or protocol change.

Red observed first: `TheRunsFirstCut_MakesItsWorldTheContinueTarget_EvenWhenAnotherWorldIsChosen` and
`TheContinueAfterAPlayedRun_OpensTheRunThePlayerJustLeft` fail on the pre-fix tree (the pointer stays on
the earlier world and the Continue resolves to it) and pass after — 36/36 focused.

## Acceptance

Batch `20261002-l` re-runs the shape that failed (three clients): start a run, mutate, the two members
leave, the host leaves and Continues, the members re-enter. Expected: the host reopens **the run's own
world** (the Debug `Continuing CUO world w-…` line names it, and `index.json` names it from the first cut
on); no `[LayerMod] baseline divergence`; `EnemySnapshot` binds on both members
(`snapshot applied … mapping=True`) across at least two repair cycles; the three censuses/health sets
agree. The `enemy-snapshot-binding-recovery` rows 3/4/8 are re-judged in the same window.

## Limits

- The pointer moves on the FIRST cut only: a world chosen in the Worlds page after that first cut wins
  over the run the player is in (the picker is the explicit choice). The first cut may be an interval
  autosave, not a player action.
- A run that crashes before its first cut leaves the pointer where it was — correct, since the folder
  holds no snapshot, but "Continue opens a world you never played" stays possible until a cut lands.
- The members still regenerated before the host's restored checkpoint arrived (it reached them at
  16:59:45.9, after their 16:59:29–44 generation). With the pointer fixed the two baselines coincide, so
  this run's shape is fixed; the general guarantee is the follow-up ticket
  `done/reenter-baseline-adoption.md`.
