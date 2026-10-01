# Acceptance record — Host block damage reports: two native `DamageBlock` callers are not hooked

- Ticket: `unhooked-damage-block-callers` — verdict: **stays in `review/`** (rows 1, 3, 5, 6 pass; row 4 unproven as noted; row 2 is open — no reachable spider-burrow path on this machine)
- Batch: `20261002-b` — tickets `unhooked-damage-block-callers`, `block-damage-table-capacity-alignment`
- Commit: `9c1b8ae8` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+9c1b8ae89c27609f9d31bf23bc730dc49119bfbd`
- Run: 2026-10-02, 00:35–00:50 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `b-crush-evidence.txt` (the excerpted lines of both crushes, all three clients), `b-crush4-host-place.json`, `b-crush4-host-read.json`, `b-crush5-guest-place.json`, `b-crush5-guest-read.json`, `b-health1-host-d0..d4.json`, `b-blockcensus-host-d1b.json`, `b-arm-host/guest/alt.json`, `probe-crush-place.cs`, `probe-crush-read.cs`, `probe-health1.cs`, `probe-blockcensus.cs`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host walks over a `health <= 1` block (footstep crush): the guest hears the same break and receives the damage, not only the air write | machine (+ audible residual) | **pass** | staged `thinice` under the host's feet at (511,992)-(513,992): the host's roll reported — `origin=OnBlockDamaged event=Break` ×3 at 00:46:40.594–.596 and `FlushPendingBlockBreak Committed(0+0)` at 00:46:40.611; the guest presented the same three cells through the game's own damage roll — `[BlockBreak] presenting a relayed break at (511,992): the block goes through the game's own damage roll, so its hit/step sounds and its break particles play here.` ×3 at 00:46:40.614–.622 (`b-crush-evidence.txt`) |
| 2 | Host's spider burrows through a wall | machine | **open** | no reachable spider-burrow path on this machine (declared before the batch; unchanged) |
| 3 | Guest's own footstep crush, host listens | machine | **pass** | staged `thinice` under the guest's feet at (513,947)-(515,947): the guest's roll reported — `origin=OnBlockDamaged event=Break` ×3 at 00:48:31.510–.517 and `FlushPendingBlockBreak Committed(0+0)` at 00:48:31.536; the host presented the same three cells at 00:48:31.531–.539 and answered the guest's reports — `[BlockSync] answered 76561199526807662's report at (513,947) with the authoritative block 0.` at 00:48:31.566–.584 (`b-crush-evidence.txt`) |
| 4 | A remote apply (the CUO applier's own `DamageBlock` roll): no report, no echo | machine | **unproven** | the damage half held: no applying side emitted an `origin=OnBlockDamaged` roll for a relayed cell, and no damage report returned to the actor. The "no echo" half is not judged: the third client's write bookkeeping recorded pending *write* reports for the relayed cells (`[BlockSync] host answered (513,947) — dropped the pending report (0 left).`, third-client log 00:48:31.565) and the host answered them (`[BlockSync] answered 76561198863287957's report at (513,947) with the authoritative block 0.`, host log 00:48:31.566). The run could not attribute those entries to the presentation's own write or to an independent local write, so the mechanism the row names (`WorldEventSync.OnBlockSet`'s early return) is left open |
| 5 | Third peer | machine | **pass** | the third client presented every relayed cell of both crushes — (511..513,992) at 00:46:40.610–.619 and (513..515,947) at 00:48:31.560–.572 (`b-crush-evidence.txt`) |
| 6 | Report volume | machine | **pass** | each crush produced exactly three rolls/reports, one per crushed cell (host ops 125/127/129; guest ops 2305/2307/2309) and exactly three presentations per peer; no per-frame stream was observed |

## Residuals for the user

- The audible half of rows 1, 3 and 5: listen to a non-destroying client while the other side crushes. The
  run's evidence is the peer's own presentation line naming the sounds and particles; audio itself is not
  recordable by the run.

## Limits

- **The staging substitution is this record's central limit.** The shipped game cannot generate any health-1
  vanilla block: `thinice` (28) and `powdersnow` (29) are the only two (`WorldGeneration.cs:576-594`), and
  `amountOfLayers = 5` is hard-coded in the same file, so the snow biome behind depths 5-7 is unreachable
  (`skiplayer` from depth 4 wraps to depth 1). Every reachable layer censused `health1: 0`
  (`b-health1-host-d0..d4.json`), the probe validated by `b-blockcensus-host-d1b.json` (`nonAir: 326223`,
  the block table resolving `28:1` and `29:1`), and a tree-wide search found no writer of block ids 26-29.
  The run therefore placed the game's own thin ice into the three foot cells through
  `WorldGeneration.SetBlock` — the write path CUO itself relays, so every peer received and applied the
  placement (`Block placed at …, type 28`) — and let `Body.HandleGroundedState`'s native roll run untouched.
- A crush always breaks (health 1 against damage 1), so these rows observe the break/relay shape; a
  surviving partial-damage convergence is not part of them.
- One session is not a race proof; each row was read from the state the ends converged to.
