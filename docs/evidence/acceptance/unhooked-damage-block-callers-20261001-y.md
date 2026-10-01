# Acceptance record — Host block damage reports: two native `DamageBlock` callers are not hooked

- Ticket: `unhooked-damage-block-callers` — verdict: **stays in `review/`** (rows 1, 3–6 unproven — the crush could not be staged in this world; row 2 stays open)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-crush-host-find.json`, `y-crush-host-find2.json`, `y-crush-guest-find.json`, `y-crush-host-log.txt`, `y-crush-guest-log.txt`, `y-crush-alt-log.txt`, `y-crush2-host-log.txt`, `y-crush2-guest-log.txt`, `y-crush2-alt-log.txt`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush): the guest hears the same break and receives the damage | machine | **unproven** | the setup was never staged: the new `crush-find` recipe returned `candidates: 0` / `cells: []` on the host at radius 60 and 120 and on the guest at radius 60 (`y-crush-host-find.json`, `y-crush-host-find2.json`, `y-crush-guest-find.json`; `skippedOutside: 0`, so the ±120 box sat fully inside the 1024×1024 world), and again at radius 200 in the layer entered later (`y-crush2-*-find200.json`). No crush rolled, so the report cadence could not be read (0 matching lines on every client after each attempt: `y-crush-host-log.txt`, `y-crush-guest-log.txt`, `y-crush-alt-log.txt`, `y-crush2-*-log.txt`). |
| 3 | Guest's own footstep crush, host listens | machine | **unproven** | same setup gap on the guest. |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll) | machine | **unproven** | not driven (no remote apply of a crush occurred). |
| 5 | Third peer | machine | **unproven** | not driven. |
| 6 | Report volume | machine | **unproven** | needs a crush to count reports per crushed cell. |
| 2 | Host's spider burrows through a wall | machine | **open** | no reachable spider-burrow path on this machine (declared before the run). |

## Limits

- The only vanilla block types whose `BlockInfo.health` is exactly 1 are `thinice` (28) and `powdersnow`
  (29) (`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:580,590`) — biome-specific tiles the
  two layers this run visited did not expose within 200 cells of either body. The recipe itself measured
  the world honestly (zero candidates, no clamping: `skippedOutside=0` at radius 60/120).
- Consequence for `block-damage-table-capacity-alignment` row 3: its live half is this crush and it stays
  unproven for the same reason.
