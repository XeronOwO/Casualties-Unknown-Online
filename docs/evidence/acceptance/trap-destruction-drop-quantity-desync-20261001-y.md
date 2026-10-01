# Acceptance record — Trap destruction drops desync in item quantity between host and guest

- Ticket: `trap-destruction-drop-quantity-desync` — verdict: **moved to `done/`** (all three rows pass)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-arm-{host,guest,alt}.json`, `y-drop2-*-read.json`, `y-drop3-*-read.json`, `y-drop4-host-break.json`, `y-drop4-guest-f01..f08.png`, `y-drop6-guest.png`, `y-fresh-screen.cs` — in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host destroys the support block under a jump pad: both ends hold the SAME drop set immediately — same count, same item identities | machine | **pass** | three pads destroyed this run. Pad cell (478,901): the same three ids on all three clients — `circuitboard 1293106137027`, `scrapmetal 1288811169731`, `scrapmetal 1284516202435` (`y-drop2-host-read.json`, `y-drop2-guest-read.json`, `y-drop2-alt-read.json`). Pad (431,886): host log `[ItemBuildingDeathDrop] … 1301696071619 / 1305991038915 / 1310286006211` + `FlushPendingBlockBreak … Committed(0+3)`, materialized on both peers. Pad (459,872): `scrapmetal 1297401104323` on all three. |
| 2 | The guest's view catches up immediately, not through the periodic keyframe | machine | **pass** | host capture 22:18:26.331/.332 and `FlushPendingBlockBreak` 22:18:26.353 vs guest `[ItemSpawn] materializing …` 22:18:26.403/.409/.413 and alt 22:18:26.404/.409/.413 — 72–80 ms, far inside the item keyframe's 5–30 s band (`y-drop4-host-break.json` plus the three clients' `BepInEx/logs/latest.log`). |
| 3 | The drops carry their fresh initial state | machine | **pass** | with the game's own setting armed on all three (`item-floating value=1` → `armed=true`, `y-arm-*.json`), the pad at (459,872) destroyed by a host standing 6.16 units away produced `scrapmetal 1297401104323`, read `fresh=true` on ALL THREE clients (`y-drop3-{host,guest,alt}-read.json`); the pad at (305,867) left three fresh items on the guest (`y-fresh-screen.cs` → `fresh: 3`), read in `y-drop6-guest.png`. |

## Limits

- The game attaches `FreshItemDrop` only while the DESTROYER's body is within 8 world units of the destroyed
  entity (`BuildingEntity.cs:74`, used at `:84/:107/:118`). The run's first pad was destroyed from 9.1 units
  and its drops carried no fresh component on any client — game behaviour, not a sync defect; the ticket's
  row 3 was judged from the pads destroyed inside that radius.
- Drop composition is random per pad (one run gave one scrapmetal, another three items); the rows judge
  identity and agreement, never a fixed set.
- One session is not a race proof; each row reads the state the ends converged to.
