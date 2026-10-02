# Acceptance record — The host classifies world-generated enemies as runtime spawns

- Ticket: `enemy-runtime-spawn-classification` — verdict: **back to `todo/`**
  (`- Status: Todo — Rejected (row 1: the in-session repair still logs `generation spawn pairing failed`
  and `mapping=False`)`)
- Batch: `20261002-f` — tickets: `enemy-runtime-spawn-classification`
- Commit: `8d70de3659d1250911610613c761ab6df0db48c8` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion 0.1.0+8d70de3659d1250911610613c761ab6df0db48c8`
- Run: 2026-10-02, one session (host on the physical machine, guest and alternate in their sandboxes) ·
  Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`,
  `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `f-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | World entry: generation enemies are not classified as runtime spawns; guests bind the generated set | machine | **fail** | entry half passes (`f-entry-host.log`, `f-entry-guest.log`, `f-entry-alt.log`); every in-session repair then logs `generation spawn pairing failed` + `mapping=False` on both guests (`f-repair-host.log`, `f-repair-guest.log`, `f-repair-alt.log`) |
| 2 | Enemy census on every client | machine | pass | `f-census-1-*.json`, `f-census-2-*.json`, `f-census-compare.txt`, `f-census-compare-1.txt` |
| 3 | Trader census before/after the entry backfill | machine | pass | trader rows of `f-census-compare*.txt`; zero `cannot create trader` in `f-entry-*.log` and `f-repair-*.log` |
| 4 | A genuine runtime creation | machine | pass | `f-spawn-1-host.json`, `f-spawn-host.log`, `f-spawn-guest.log`, `f-spawn-alt.log`, `f-census-3-*.json` |
| 5 | Regression half | machine | pass | `f-build.log`, `f-full-test.log`, `f-format.log`, `f-deploy.log`, `f-verify-deploy.log` |

## Row 1 — what passed and what failed

**Entry (passed).** The host bound the whole generation set as generation animals: `f-entry-host.log`
carries 85 `[Enemy] host bound generation animal …` lines (72 `shadecrawler` + 13 `trader`), zero
`host bound runtime spawn` and zero `[EntitySpawn] reporting`. Both guests logged
`[Enemy] guest froze 85 enemy copies at generation finish` and
`[Enemy] snapshot applied: 85 generated bound, 0 runtime spawns, mapping=True` (10:12:57 and 10:13:01),
with zero `generation spawn pairing failed` and zero `cannot create trader` at that edge
(`f-entry-guest.log`, `f-entry-alt.log`). The host also sent `85 enemies, 0 runtime spawns` in its entry
snapshots.

**In-session repair (failed).** The host's repair snapshot repeats every 60 s (`f-repair-host.log`:
`[Enemy] snapshot sent …` at 10:14:01, 10:15:01, 10:16:01, 10:17:01). Every one of those four applies
ends the same way on BOTH guests (`f-repair-guest.log`, `f-repair-alt.log`):

```text
[WRN] [Enemy] generation spawn pairing failed (85 host vs 85 guest generated enemies) — generated copies stay local (generation divergence); runtime spawns are still bound.
[INF] [Enemy] snapshot applied: 0 generated bound, 1 runtime spawns, mapping=False.
```

The row's expected `zero generation spawn pairing failed` therefore does not hold once the session
outlives the entry edge: the repair fails on 4/4 cycles.

Mechanism, read from the code: `EnemySyncCoordinator.OnEnemySnapshotReceived` re-pairs the generated
baseline through `EnemySpawnArbitration.TryPair` — the host's bind-time anchors against the guest's
**current** positions, index-by-index, all-or-nothing inside `PairTolerance` 0.5. After the entry bound
the guest copies, the 20 Hz drive moves them to the host's live positions: at census #2 every guest
`shadecrawler` position equals the host's live position (nearest-neighbour mean 0, max 0.18) while the
host side of the pairing is still the anchor, so any moved animal fails the whole set. The failure also
clears `_mappingEstablished`, which switches off the 20 Hz runtime-copy bind (`TryBindRuntimeSpawns`);
the row-4 creation still bound, through the repair snapshot's creation-key match in
`MaterializeRuntimeSpawns`, and produced no duplicate.

## Row 4 — positive control detail

The host instantiated one `shadecrawler` outside generation (`f-spawn-1-host.json`, `ok:true`,
`animal:true`); the host logged `[EntitySpawn] reporting shadecrawler … (animal — recovery owned by the
enemy domain)` and `[Enemy] host bound runtime spawn … (prefab shadecrawler)`, and shipped it in the
snapshots (`86 enemies, 1 runtime spawns`). Each peer created exactly one copy
(`[EntitySpawn] created shadecrawler at (2.00, 489.62) (creation …)` once per peer), the backfill bound
that copy by creation key instead of materializing a second one (no `[Enemy] materialized runtime spawn`
line), and `f-census-3-*.json` shows exactly +1 `shadecrawler` on all three clients (86 = 73 + 13) with
the new copy carrying `RuntimeEntityCreation` everywhere and `RemoteEnemyDriver` on both guests, at
identical positions.

## Residuals for the user

None: every row is a machine row judged from this run's own logs and probe dumps.

## Limits

- One session: the repair failure recurs over four consecutive 60 s cycles, but this is not a race
  proof and not a multi-session proof.
- The census is a point-in-time dump, and both censuses are post-entry; the literal pre-backfill
  instant was not captured. The "before/after" claim rests on the entry snapshot carrying
  `0 runtime spawns` (nothing to materialize) plus zero materialization attempts and zero failures on
  any client.
- Only the host-created runtime-spawn direction was staged; the guest-created direction was not run.
- The pre-fix comparison is batch `20261002-e`'s recorded evidence
  (`docs/evidence/acceptance/20261002-e-scope.md`), not re-run here.
