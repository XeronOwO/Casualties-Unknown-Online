# Acceptance record — Enemy snapshot binding has no recovery path (batch 20261002-l)

- Ticket: `enemy-snapshot-binding-recovery` — verdict: **rows 3, 4 and 8 now pass; row 2 stays unproven → back to `todo/`** (no empty-host-table window is stageable in this setup)
- Batch: `20261002-l` — tickets `layer-mod-baseline-divergence-on-continue`, `enemy-snapshot-binding-recovery`
- Commit: `b6981ed4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+b6981ed43fe531956d09b1874bc53f7c6f09b75d`
- Run: 2026-10-02, 17:52–18:00 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Alt: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: `l-*` in the directory named by `acceptance-artifacts-dir`, from the same session as
  `docs/evidence/acceptance/layer-mod-baseline-divergence-on-continue-20261002-l.md`; this record cites
  artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 3 | A late joiner receives the full enemy snapshot | machine | **pass** | both members left the world, the host left and Continued (restoring the run's own world), and the members re-entered on the host's invite; the host sent `76 enemies, 0 runtime spawns` at 17:55:32 (`l-host-2.log`) and the repair cycle bound them: guest and alt `snapshot applied: 76 generated bound, 0 runtime spawns, mapping=True` at 17:56:26 (`l-guest-2.log`, `l-alt-2.log`). The first pass at 17:55:39 applied `0 generated bound … mapping=True` (their copies were not frozen yet) — the same entry-edge shape batch `20261002-k`'s row 1 recorded as "an empty pass, then the first repair repeat bound the whole set" |
| 4 | Reconnect-while-InWorld re-fans the same group | machine | **pass** | four consecutive cycles each re-fanned and re-bound the same 76: host `snapshot sent … 76 enemies, 0 runtime spawns`, both members `snapshot applied: 76 generated bound, 0 runtime spawns, mapping=True` at 17:56:26, 17:57:26, 17:58:26 and 17:59:26 (`l-host-3.log`, `l-guest-3.log`, `l-alt-3.log`); zero `pairing failed` and zero `mapping=False` since the re-entry mark (`l-*-negative-B.log`) |
| 8 | Peers agree on the shared enemy id set and terminal health across two live clients | machine | **pass** | census after the re-entry: 76 animals on all three clients (71 shadecrawler + 5 trader), `counts-match-host: yes`, identical trader position sets (`l-census-*.json`, `l-census-compare.txt`); health: per-prefab counts and per-prefab health multisets identical across the three (`verdict: IDENTICAL`; `l-health-*.json`, `l-health-compare.txt`) |
| 2 | An empty host enemy table sends nothing | machine | **unproven** | no empty-table window was isolated in this session: every send carried 76 enemies, and nothing left the host's table empty at a send moment. Missing setup named: a member entering while the host's table is still empty (the host's generation window), which this staging route does not reach |

## Limits

- Rows 1, 5 and 6 stand from batch `20261002-k` and were not re-run; row 7 belongs to
  `review/enemy-hit-determination-local.md`.
- The "late joiner" shape is a member that had been in the world, left it and re-entered — the shape the
  `20261002-k` record judged rows 3/4/8 in. A client that has never seen the world was not staged.
- Adjacent observation outside the rows' window: the alt's **entry** snapshot (17:54:26.1) logged
  `generation spawn pairing failed (76 host vs 76 guest generated enemies)` and applied
  `0 generated bound … mapping=False` — it arrived before that client's generated copies were frozen at
  their spawn spots. The repair healed it (mapping true by 17:55:39; 76 bound at 17:56:26). Recorded, not
  judged: it is the same entry-edge transient family as row 1's empty pass.
- The health probe reports the **prefab** id (`shadecrawler` / `trader`), not a unique network entity id,
  so "the shared enemy id set" is judged as per-prefab counts plus the per-prefab health multiset — the
  strongest comparison this machine's probes support.
