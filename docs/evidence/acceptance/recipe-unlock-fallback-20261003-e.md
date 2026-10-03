# Acceptance record — Recipe unlock has no fallback or backfill

- Ticket: `recipe-unlock-fallback` — verdict: **stays in `review/`** (rows 1–5 and 7 pass; row 6, gap 1's
  open-panel half, gap 4 and gap 6 stay `unproven`)
- Batch: `20261003-e` — tickets `recipe-unlock-fallback`, `trade-domain-dual-side-runtime`,
  `guest-report-fallback-first-resend`, `sync-cadence-review` (scope:
  `docs/evidence/acceptance/20261003-e-scope.md`)
- Commit: `eef70481` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+eef70481fef869766c530da6d234c9f96f637ff1` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, 09:22–09:36 +08:00 · Host: physical machine (Steam, app id 4576510) · Guest: Steam1
  sandbox; two clients, one world, one lobby
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: the `s1*` and `s2*` ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest uses a blueprint; the one-shot report is dropped — the host learns the unlock through the guest's set re-report and both ends converge | `machine` | **pass** | the guest's `item-use type=blueprint` at 09:24:39.27 unlocked locally (its table 2 → 3, `s1c-craft-table-guest.json`; use result `s1b-blueprint-use-guest.json`) while the host's inbound was blacked out 09:24:38.787–39.272 (`s1b-blackout-on/off-host.json`) and its table stayed 2 (`s1c-craft-table-host.json`); the guest's re-report `[Crafting] re-reported this guest's 3 unlocked recipe(s) to the host.` at 09:25:40.168 (**60.9 s later**, the steady 60 s step), the host's merge `76561199526807662's unlock set: 3 reported, 1 this host had not learned.` + `recipe 46 unlocked` at 09:25:40.182–185 (`s1e-guest-rereport.txt`, `s1e-host-applied.txt`); the host's set delivered 3 at 09:26:14.236 and the guest applied it at 09:26:14.454 (`s1f-host-set-sends.txt`, `s1f-guest-set-applies.txt`); both tables read 3 |
| 2 | Host unlocks; the relay is dropped — the guest converges through the host's set on its repair cycle | `machine` | **pass** | the host's `item-use type=blueprint` at 09:27:52.237 with the guest's inbound blacked out (`s2b-*`); the host's table 4 and the guest's 3 (`s2c-craft-table-*.json`, `s2c-host-crafting-log.txt`); the host's repair set `sent 4 unlocked recipe(s)` at 09:28:18.753 and the guest's `applied 4 unlocked recipe(s) from …'s set.` at 09:28:18.983 (`s2d-*`) |
| 3 | Late joiner receives the current set in the entry group, with no per-recipe alert | `machine` | **pass** | the guest left and rejoined the running lobby at 09:28:45–51 (`s3a-*`, `s3b-*`); the host's entry send `sent 4 unlocked recipe(s)` at 09:28:57.771 (`s3c-host-entry-sends.txt`) and the guest's table read 4 immediately after the entry (`s3c-craft-table-guest.json`), through the set path only — no `recipe N unlocked` line on the guest |
| 4 | Reconnect: the same entry group, idempotent apply | `machine` | **pass** | two further rejoins (09:29:33–40, 09:30:59–31:06) re-delivered the same four-index set; the guest's table stayed 4 and every apply is the same `INT = 0` write (the repeated `applied … set` lines are listed with row 5) |
| 5 | Duplicate delivery is idempotent | `machine` | **pass** (limit) | the same set was delivered repeatedly — 2, 2, 2, 3 recipes at 09:23:32.661, 09:24:13.711, 09:25:14.059, 09:26:14.454 (`s1f-guest-set-applies.txt`) — with no repeated alert and no state change; a forced duplicate guest→host set was not staged (the guest's pending entry cleared on the host's answer), so the merge's diff-before-apply is evidenced by the same log pair as row 1 |
| 6 | Host rejects / does not have the recipe | `machine` | **unproven** | stock content's recipe table always holds an index a guest can unlock, and the "no live table" branch needs the host out of the world while the guest stays in it; the run produced neither, so no refusal was observed |
| 7 | CraftReport dropped at the same time — the item facts still converge through the unchanged keyframe/snapshot paths | `machine` | **pass** | the guest crafted recipe 0 with the host's inbound blacked out 09:26:37.610–38.043 (`s1h-*`; rope id `23041378414`); the host logged `[CharSync] divergence for …'s rope (id 23041378414): a new carried item the fact table never saw — a pickup without an event sync (the 1 Hz snapshot carried it).` at 09:26:38.585 (0.6 s later, `s1j-host-rope-log.txt`) and the rope is in the host's transfer table (`s1i-host-read.json`) |

## Declared coverage gaps (the ticket's own list)

| Gap | Verdict | Evidence |
|---|---|---|
| 1 — the adapter's Unity half: the real table read, the silent batch write, the open-panel refresh | **pass** (machine) / **unproven** (visual) | the write and the set apply are the `recipe 46 unlocked` and `applied the recipe-unlock set: N recipe(s), 0 refused.` lines of rows 1–2; the open-panel refresh half was not staged — the receiving panel was not held open across a delivery and no frame was captured |
| 2 — the protobuf round trip in a real two-process session | **pass** | both shapes crossed two processes: the per-index relay and the absolute sets of rows 1–4 |
| 3 — the real host 60 s cycle that calls the repair group | **pass** | the host's set sends at 09:24:13.506, 09:25:13.841, 09:26:14.236, 09:27:15.617, 09:28:18.753 — 60–63 s apart (`s1f/s2d-host-set-sends.txt`) |
| 4 — the menu-armed pending branch | **unproven** | the window has no runtime seam: the guest must arm before its session is active and the run cannot hold that state |
| 5 — the suggested late-joiner check | **pass** | row 3 |
| 6 — the per-index give-up warning | **unproven** | it needs an index the host's table can never carry (a divergent content/mod fixture); stock content has none |

## Limits

- One session; the steady step was measured at 60.9 s and the repair cycle at 60–63 s.
- Every blackout window was a short inbound cut (0.1–1.9 s), lifted inside the 15 s host-silence watchdog.
- No third client was present: row 1's "third parties" is covered by the late-joining member (row 3), not
  by a third participant.

## Residuals for the user

- None: every unjudged item is a capability or fixture limit named above, not a judgement a person must
  make.
