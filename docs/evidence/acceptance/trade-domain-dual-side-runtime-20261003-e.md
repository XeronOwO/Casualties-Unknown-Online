# Acceptance record — Trade domain dual-side runtime pass

- Ticket: `trade-domain-dual-side-runtime` — verdict: **stays in `review/`** (rows 1–7 pass; rows 8–9
  `unproven` with their fixtures named)
- Batch: `20261003-e` — tickets `recipe-unlock-fallback`, `trade-domain-dual-side-runtime`,
  `guest-report-fallback-first-resend`, `sync-cadence-review` (scope:
  `docs/evidence/acceptance/20261003-e-scope.md`; the row table was written in that page before the run,
  workflow §1)
- Commit: `eef70481` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+eef70481fef869766c530da6d234c9f96f637ff1` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, 09:22–09:36 +08:00 · Host: physical machine (Steam, app id 4576510) · Guest: Steam1
  sandbox; two clients, one world
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: the `s0*`, `s3*`, `s8*`, `s10*`, `s11*`, `s12*` ids below, in the directory named by
  `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest gives an item to the nearest trader: the host executes the trader-side credit and broadcasts, and both ends read the same credit/stock | `machine` | **pass** | guest `trade-drive mode=give type=neuralbooster` at 09:32:21.662 credited locally 0 → 50 (`s8a-give-guest.json`); the guest's `[Trade] report action=GiveItem trader=(7.0,450.0) item=neuralbooster.` 09:32:21.945; the host's `[Trade] executed action=GiveItem … accepted=True` 09:32:21.979; the guest's authoritative state 09:32:21.998 (`s8a-*-trade-log.txt`); both ends read valueGiven/totalValueGiven 50, stock 11 (`s8a-list-*.json`) |
| 2 | A guest buys a stock entry: the item lands in the guest's inventory, the host charges and removes the entry, and the bought item is registered on the host under the buyer | `machine` | **pass** | guest `mode=buy index=0` at 09:32:34.346 → naltrexone, price 8, stock 11 → 10, product id `57401116782` (`s8b-buy-guest.json`); the host's `[Trade] executed action=Purchase … accepted=True` 09:32:34.634 (`s8b-host-trade-log.txt`); that id is in the host's transfer table (`s8b-host-read.json`, `host-registered=True`); both ends read 42/10 (`s8b-list-*.json`) |
| 3 | A host-local give/buy: the host broadcasts immediately and the guest converges without acting | `machine` | **pass** | host give at 09:33:56.620 (`s10a-give-host.json`), `[Trade] host broadcast action=GiveItem trader=(7.0,450.0).` 09:33:56.901 and the guest's `[Trade] state received trader=(7.0,450.0) …` 09:33:56.918 (**17 ms** later, `s10a-host-broadcast.txt`, `s10a-guest-state.txt`); both ends read 52/10 |
| 4 | A refused purchase: the acting guest destroys its locally bought copy and the authoritative overwrite restores stock and credit on both ends | `machine` | **pass** | with the guest's inbound blacked out the host bought index 0 at 09:35:23.05 (`s12-host-buy.json`); the guest bought its stale copy at 09:35:23.174 (product `61696084078`, `s12-guest-buy.json`); the host's `[Trade] executed action=Purchase … accepted=False` 09:35:23.226 and the guest's `[Trade] rejected purchase — rolling back the locally bought item naloxone.` 09:35:23.246 (`s12-host-executed.txt`, `s12-guest-rollback.txt`); the id is absent from the guest's local tree (`s12-guest-local.json`) and both ends read 41/8 (`s12-list-*.json`) |
| 5 | A broadcast one member missed is healed by the host's 5 s absolute fallback, no reconnect | `machine` | **pass** | the host's purchase at 09:34:41.526 (`[Trade] host broadcast action=Purchase` 09:34:41.592) was dropped by the guest's blackout; the guest read 52/10 stale (`s11-list-guest-during.json`); the next fallback `[Trade] state received trader=(7.0,450.0) rep=114 items=9` arrived at 09:34:46.780 (**5.2 s** after the lift, `s11-guest-state.txt`) and the guest read 47/9, matching the host (`s11-list-*.json`) |
| 6 | A member that was not in world when an interaction happened converges through the same fallback — there is no dedicated world-entry send | `machine` | **pass** | first entry: the guest's nearest-trader read 0 stock right after entry (`s0-trade-guest.json`) and 11 at the next read (`s0b-trade-guest.json`); the staged re-entry at 09:28:51 read 0 at 09:28:56.98 (`s3c-trade-guest-entry.json`) and the receiver's `state received` lines began at 09:29:05.958 and repeated at a **5.016 s** cadence, with the read at 11 at 09:29:13.866 (`s3d-*`) |
| 7 | The host resolves the report to the acting side's own trader (the position key) | `machine` | **pass** | the guest's report and the host's executed line name the same trader coordinates (7.0,450.0) in rows 1–2 and in the host-local path's broadcast (row 3) |
| 8 | Trader recruit (#59): a living player at a friendly trader revives a dead teammate in place and grants 1–3 trader-stock items | `machine` + `visual` | **unproven** | the run did not produce the setup: a dead target visible to a requester standing inside a trader's 8-unit range, plus the Online UI recruit control drive. This world's nearest trader already passed the gate (reputation 100, hostility 0, build health 1250 — `s0-trade-host.json`); the missing fixture is the death + proximity + control chain |
| 9 | Hostile trader swing (#93): the one-shot swing presentation is replayed on the clients that did not run it | `machine` + `visual` | **unproven** | no hostile-trader or swing drive exists in the batch's vocabulary; the performance needs a frame at the swing moment. The fixture is named for the next cycle |

## Limits

- One session, one world; the trade fallback was observed at its 5 s base and at a stretched ~8.6–10.0 s
  cadence under the run's ordinary load (no max-pressure injection).
- A give above the trader's lifetime credit cap is refused (`s10b-give-host.json`, credited=false); the
  lost-broadcast row therefore used a purchase.
- The refused-purchase round relies on the guest's local stock being stale between the hidden host buy and
  the guest's own buy (a ~1.4 s window); the fallback tick did not land inside it.
- No third client was present; the receiving peer in rows 1–7 is the guest.

## Residuals for the user

- None: the two unproven rows are fixture gaps named above, not subjective judgements.
