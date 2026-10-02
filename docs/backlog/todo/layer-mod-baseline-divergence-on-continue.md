# Layer-modifier baseline diverges after a layer-end Continue

- Status: Todo
- Priority: High
- Category: Network / sync / world generation (layer modifiers)
- Source: agent acceptance batch `20261002-k` (2026-10-02) — observed while staging the late-join/reconnect rows of that batch; not a user report
- Related: `docs/evidence/acceptance/enemy-snapshot-binding-recovery-20261002-k.md`, `docs/evidence/acceptance/world-determinism-world-fingerprint-20261002-k.md`, `review/save-system-mid-run-and-layer-end.md`, `review/save-mid-run-consistent-cut.md`

## Problem (evidence)

After the host's leave-world + Continue — which restored the run's **layer-end cut** and moved the session to
layer 1 (`Projected kernel run baseline (run 1, layer 1)`, `Captured world baseline (1024x1024) — a restored cut
is pending; … the runtime-entity table is reset`) — **both members** repeated, every 10 s:

```text
[WRN] [LayerMod] baseline divergence — local segment start 5200E7D148E10BB7426A68F7E2407117 vs host's
      E76DFACE27BBFC1FC9FFB1C2648822EB (world effects may diverge).
```

The divergence is not cosmetic in that state: the host's enemy generation set stayed at **74** animals
(69 shadecrawler + 5 trader) while both members held **85** (78 + 7), so `EnemySnapshot`'s all-or-nothing
pairing failed on every repair cycle (`generation spawn pairing failed (74 host vs 85 guest generated
enemies)` + `snapshot applied: -11 generated bound, 0 runtime spawns, mapping=False`), and the members kept
their locally regenerated world. A member-side replay line reads `[LayerMod] guest replay index=-1 depth=0`.

Observed with artifact `0.1.0+393d79a8c0d8f4a20320b667f6122884daa656ae` (commit `393d79a8`); artifacts
`k-F-baseline-guest.log`, `k-F-baseline-alt.log`, `k-F-snapshot-latest-*.log`, `k-census-F-*.json` in the
local acceptance artifact directory.

## Open questions for the fix cycle

- Is a member's layer regeneration supposed to replay the host's layer-modifier state (the `replay index=-1`
  line suggests the replay found no entry), or is a divergence expected here and only warned about?
- Does the restored cut carry the layer-modifier segment, and if it does, why does the member not adopt it?
- The 74-vs-85 enemy difference may be a consequence of the divergent modifiers or of live deaths inside the
  restored world; attribute before choosing the fix.
- The same state is the late-join/reconnect environment for `todo/enemy-snapshot-binding-recovery.md`
  (rows 3/4/8 failed there); the two tickets should be worked together.
