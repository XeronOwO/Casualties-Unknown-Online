# Acceptance record — The host classifies world-generated enemies as runtime spawns

- Ticket: `enemy-runtime-spawn-classification` — verdict: **moved to `done/`**
- Batch: `20261002-g` — tickets: `enemy-runtime-spawn-classification`
- Commit: `9dd120fea21c4415ccc58f59d66d8bf684775364` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion 0.1.0+9dd120fea21c4415ccc58f59d66d8bf684775364`
- Run: 2026-10-02, one session (host on the physical machine, guest and alternate in their sandboxes) ·
  Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`,
  `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `g-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | World entry and the in-session repair: generation enemies are not runtime spawns; guests bind the generated set | machine | **pass** | `g-entry-host.log` (86 `host bound generation animal`, zero `host bound runtime spawn`), `g-entry-guest.log` / `g-entry-alt.log` (`86 generated bound, 0 runtime spawns, mapping=True` at 11:13:17 and 11:14:17), zero `generation spawn pairing failed` and zero `mapping=False` in any of the three |
| 2 | Enemy census on every client | machine | pass | `g-census-1-*.json`, `g-census-2-*.json`, `g-census-compare.txt` |
| 3 | Trader census before/after the entry backfill | machine | pass | trader rows of `g-census-compare.txt`; zero `cannot create trader` in `g-entry-*.log` |
| 4 | A genuine runtime creation | machine | pass | `g-spawn-1-host.json`, `g-spawn-host.log`, `g-spawn-guest.log`, `g-spawn-alt.log`, `g-census-3-*.json`, `g-census-compare-3.txt` |
| 5 | Regression half | machine | pass | `g-build.log`, `g-full-test.log`, `g-format.log`, `g-deploy.log`, `g-verify-deploy.log` |

## Row 1 — what the fix changed and what this run read

Batch `20261002-f` passed the entry edge and failed every 60 s repair on both guests:
`generation spawn pairing failed (85 host vs 85 guest generated enemies)` +
`snapshot applied: 0 generated bound, 1 runtime spawns, mapping=False` — the repair re-paired the
already-bound copies against the host's bind-time anchors while the 20 Hz drive held them at their live
positions.

This run's entry: the host bound the whole generation set as generation animals (86 = 79 `shadecrawler`
+ 7 `trader`, zero runtime spawns — `g-entry-host.log`), both guests logged
`guest froze 86 enemy copies at generation finish` and then `86 generated bound, 0 runtime spawns,
mapping=True`. The one earlier line on each guest (`0 generated bound, 0 runtime spawns, mapping=True`
at 11:12:54) is the entry send that landed before the local copies existed: the empty pass now preserves
the baseline instead of clearing it, and the entry-repair schedule's first repeat bound the set 23 s
later.

The in-session repair then repeated twice inside this run (the host's `snapshot sent` at 11:13:17 and
11:14:17, one send per member):

```text
[INF] [Enemy] snapshot applied: 86 generated bound, 0 runtime spawns, mapping=True.
```

on BOTH guests, with zero `generation spawn pairing failed` and zero `mapping=False` across all three
logs. The generated reading is the copies the pass asserts as held (host facts minus the still-unbound
candidates), not the copies paired this cycle — the ambiguity a review of the fix found and this run
confirms gone: a healthy repeat no longer reads like the rejected `0 generated bound … mapping=False`
line.

## Row 4 — positive control detail

The host instantiated one `shadecrawler` outside generation (`g-spawn-1-host.json`, `ok:true`,
`animal:true` at (2.0, 470.6)); the host logged
`[EntitySpawn] reporting shadecrawler … (animal — recovery owned by the enemy domain)` and
`[Enemy] host bound runtime spawn 8DF2032D132A586:86:0 (prefab shadecrawler)`. Each peer created exactly
one copy through the creation channel (`[EntitySpawn] created shadecrawler at (2.00, 470.62)` once per
peer, no `[Enemy] materialized runtime spawn`) and bound it to the host's id
(`[Enemy] bound 1 runtime enemy copies to host ids.` once per peer). `g-census-3-*.json` shows exactly
+1 copy per client (87 = 80 + 7) with `withCreationMarker=1` everywhere and `RemoteEnemyDriver` on both
guests; `g-census-compare-3.txt` reports every position identical across the three clients and no
same-prefab pair closer than 1.5 units (no duplicate copy).

## Residuals for the user

None: every row is a machine row judged from this run's own logs and probe dumps.

## Limits

- One session: the repair now passes over two consecutive cycles (11:13:17, 11:14:17) plus the entry
  edge, but this is not a race proof and not a multi-session proof.
- The census is a point-in-time dump; the literal pre-backfill instant was not captured. The
  "before/after" claim rests on the entry snapshot carrying `0 runtime spawns` (nothing to
  materialize) plus zero materialization attempts and zero failures on any client.
- Only the host-created runtime-spawn direction was staged; the guest-created direction was not run.
- The repair is also the only path that establishes a member's baseline from scratch in this run's
  shape (both guests paired from the entry-repair repeat); a late joiner whose host animals have
  already wandered pairs on the same anchors and keeps the pre-existing late-anchor limit recorded on
  the ticket.
- The pre-fix comparison is batch `20261002-f`'s recorded evidence, not re-run here.
