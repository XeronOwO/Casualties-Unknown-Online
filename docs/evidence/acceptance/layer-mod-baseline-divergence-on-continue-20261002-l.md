# Acceptance record — A Continue after a run opens a stale world (the layer-mod baseline then diverges)

- Ticket: `layer-mod-baseline-divergence-on-continue` — verdict: **fixed and verified; moved to `done/`**
- Batch: `20261002-l` — tickets `layer-mod-baseline-divergence-on-continue`, `enemy-snapshot-binding-recovery`
- Commit: `b6981ed4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+b6981ed43fe531956d09b1874bc53f7c6f09b75d` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-02, 17:52–18:00 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Alt: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: `l-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Claim | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The Continue target moves to the world a run plays, on that world's first committed cut | machine | **pass** | pointer before the run `w-20261001-6986` (`l-index-before.json`); the run created `w-20261002-5937` (`l-world-host.log`: `This run writes into world w-20261002-5937`) and the pointer still named the old world at entry; the host's leave-world wrote the run's first cut (`mid-run-20261002-095447.cuoz` in that world's `backups/`) and the pointer then read `w-20261002-5937` (`l-index-after-leave.json`) |
| 2 | The native Continue opens that world | machine | **pass** | `l-continue-host.log`: `Continue restored world w-20261002-5937: mid-run cut taken at frame-end, revision 729, layer 0, from the live snapshot`; `Continuing CUO world w-20261002-5937: …`; `Applied the RESTORED run baseline at the continue click (16 RNG bytes, depth 0)` and at the boundary `(depth 0, override 0, traveled 0)` |
| 3 | The members regenerate the same world and baseline — no layer-mod divergence | machine | **pass** | host `[LayerMod] enter state=77B6B8D88F4637F29B9F49158F5E877C chance=40 depth=0`, `picked=5 prefix=积水`; guest and alt `[LayerMod] guest replay index=5 depth=0 entryState=77B6B8D88F4637F29B9F49158F5E877C` and `applied host modifier 5` (`l-host-2.log`, `l-guest-2.log`, `l-alt-2.log`); zero `baseline divergence`, `pairing failed` or `mapping=False` on any client since the re-entry mark (`l-*-negative-B.log`) |
| 4 | The run's own entry path is not regressed | machine | **pass** | entry census 76 animals (71 shadecrawler + 5 trader) on all three clients with identical trader positions (`l-census-entry-*.json`, `l-census-entry-compare.txt`) |

## What this fixes, restated against the batch that found it

Batch `20261002-k`'s host Continue had restored `w-20261001-6986` (a 2026-10-01 layer-end cut, layer 1,
traveled 307) while the run played `w-20261002-62b8`, so the host's layer-1 baseline `F8A3757E…` never
matched the members' `30BFAA…`. In this run the pointer moved on the run's own first cut, the Continue
restored `w-20261002-5937`'s **mid-run, layer 0** cut, and all three clients generated from the same
decision entry state (`77B6B8…`) and applied the same modifier (index 5, `积水`).

## Limits

- One session, one Continue. The rule's "a picker selection made after the first cut still wins" half is
  unit-pinned, not staged live.
- The restore was a mid-run cut at layer 0 (the run had not descended a layer when the host left); a
  layer-end Continue of the run's own world is not exercised here — the pointer rule itself is
  world-level, not cut-kind-level.
- Machine facts of the session (paths, ids) live in `docs/acceptance/AGENTS.local.md` and the local
  artifact directory, never here.
