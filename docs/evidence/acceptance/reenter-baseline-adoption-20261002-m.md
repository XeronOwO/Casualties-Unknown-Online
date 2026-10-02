# Acceptance record — A member that never left the session generates before the host's restored run baseline arrives

- Ticket: `reenter-baseline-adoption` — verdict: **fixed and verified; moved to `done/`** (rows 1–6)
- Batch: `20261002-m` — ticket `reenter-baseline-adoption`
- Commit: `6dc751bc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+6dc751bcfdd1facd7afca42985bca1177d3899fd` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-02, 19:01–19:08 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Alt: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: `m-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Claim | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The invite edge sends the baseline itself, instruction first and checkpoint set right after | machine | **pass** | host `m-world-host.log` + `m-host-continue.log`: `World join sent to 2 member(s) (tutorial: False, run 1, baseline follows: True)` at the click-path entry (19:01:53.067), at the world-entry edge after the layer switch (19:02:57.020) and after the Continue (19:06:18.800) |
| 2 | A member that stayed connected adopts the host's baseline BEFORE its generation consumes anything | machine | **pass** | guest/alt `m-guest.log` / `m-alt.log`: `World join received — starting a run to follow` 19:02:57.020 → `Projected kernel run baseline (run 1, layer 1)` 19:02:57.040 → `Restored kernel checkpoint at revision 1098 (2 items, run 1)` 19:02:57.042 → `Generation stream reset to captured baseline (16 bytes: 0CAD53ACB0616E79B0CE5ADA10C4C68C)` 19:02:58.341; the host's layer-1 baseline is the same `0CAD53AC…` (`m-host-layer.log`), and the member's own previous baseline was `02FD0E74F6638E49FBD36424C5C695A1` (guest log, entry reset) |
| 3 | The member's generation is the host's world, byte for byte | machine | **pass** | `m-guest-gen.log` vs `m-host-gen.log`: guest `[GenStream] segment 6…19` equal the host's `segment 9…19` (`62F21517…`, `081D8E0C…`, `B298616A…`, `11A8F5D5…`, `8EFC87F6…`, `9D62FB93…`, `02F52C31…`, `9D15177E…`, `A6335F6D…`, `06182ABA…`, `1C10E837…`, `692FA317…`); the alt matches the same way |
| 4 | The Continue re-delivers the restored baseline to members that are out of world | machine | **pass** | `m-host-continue.log`: `Continue restored world w-…: mid-run cut taken at frame-end, revision 1428, layer 1`, `Applied the RESTORED run baseline at the continue click (16 RNG bytes, depth 1)`, `Generation stream reset (16 bytes: 7F597777…)`; guest/alt `m-*-round2.log`: `World join received` 19:06:56.913 → `Restored kernel checkpoint at revision 3020 (273 items, run 1)` 19:06:57.03 → `Generation stream reset (7F5977776B6C9FBB88A4DF6871390FBA)` 19:06:58.1x |
| 5 | Layer-mod agreement, and zero `baseline divergence` in a clean window | machine | **pass** | host `m-host-layermod2.log`: `[LayerMod] enter state=E5D3D723B117268DC2F862A15C15068F chance=40 depth=2 override=None`; guest and alt `m-*-round2-gen.log`: `[LayerMod] guest replay index=-1 depth=2 entryState=E5D3D723B117268DC2F862A15C15068F`, all three `[GenStream] done — 19 segments`; `m-*-negative-D.log`: NO MATCHING LINES on host and alt, and on the guest only the enemy-family line below |
| 6 | The run's own entry path is not regressed | machine | **pass** | entry window: all three clients reset to `02FD0E74…` and finished `19 segments` (`m-world-host.log`, guest/alt logs), no divergence or pairing line before the layer switch |

## Observation outside this ticket's rows: the enemy census/health difference

Taken ≈35 s after the guest's re-entry, i.e. before the 60 s enemy repair cycle that `20261002-l` showed
healing this binding: host 58 animals (28 shadecrawler / 21 wallbiter / 9 trader), guest 57 (29 / 19 / 9),
alt 58 (28 / 21 / 9); `m-health-compare.txt` verdict `DIFFERENT` (guest 29 / 19). The run's own log names
the producer — `[Enemy] generation spawn pairing failed (58 host vs 57 guest generated enemies) —
generated copies stay local (generation divergence)` and `snapshot applied: 1 generated bound, 0 runtime
spawns, mapping=False` — so this belongs to `todo/enemy-snapshot-binding-recovery.md` and is recorded
there. The ticket's own rows do not depend on it: the members' generation streams equal the host's byte
for byte (row 3) and the layer-mod entry states agree (row 5).

## What this fixes, restated against the batch that found it

Batch `20261002-k`'s F re-entry had both members regenerate from their own last baseline while the host's
restored state was still in flight (member reset 16:59:29.5, checkpoint 16:59:45.9). In this run the
members' `Generation stream reset` used the host's baseline the moment the promised checkpoint landed —
1.3 s after the restore, and before the generation consumed anything — and their generation matched the
host's segment for segment. The handshake path's own order (entry group before the join, no promise) is
unchanged and was not part of this staging.

## Anomaly in the first pass, isolated and attributed

The first pass left the host out of the world while the members were still generating (host leave
19:03:05; their generation ended 19:03:11.478). Their `[GenStream]` tails then counted host-wait yields
as segments (guest 51, alt 43, against the host's 19), the last empty segment overwrote the recorded
segment start (`D6848B6D0B46332A4A24DBC1E3CB786B` against the host's
`692FA3173CE7973949303E6E7C7FD415`), and both members warned `[LayerMod] baseline divergence` from
19:03:12 while applying the host's modifier authoritatively. The real segments matched the host's through
`692FA317…`, so the adoption itself held; a second pass with the host present for the whole generation
reproduced `19/19/19` segments and zero divergence. Recorded as
`done/guest-generation-segments-over-host-absence.md`.

## Limits

- The staging exercises the layer-switch and Continue invite edges. A page-chosen restore of another world
  (the Worlds page) is not staged.
- The census/health difference above was read inside the 60 s enemy repair window; it is recorded as an
  observation on the enemy-binding ticket, not judged here.
- One session. A promised set lost before the member's InWorld edge is not staged: both transports are
  reliable and ordered, so the ticket's lost-set window stays a documented limit.
- Only the Runtime seam is unit-pinned; the adapter's ordering (join before the member's `GenerateWorld`,
  the generation held until the restore) is judged here from the logs, which is what the ticket's
  acceptance asked for.
- Machine facts of the session live in `docs/acceptance/AGENTS.local.md` and the local artifact directory,
  never here.
