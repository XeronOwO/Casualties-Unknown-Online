# Acceptance record — Carried-inventory registration has no re-report

- Ticket: `carried-inventory-registration-re-report` — verdict: **stays in `review/`** (rows 1-3 need
  a controllable swallowed registration and a reconnect run; rows 4-7 are decided by this run's suites)
- Batch: `20260927-d` — the item-domain ticket that the recipe headers used to name; it is not a
  carry-relation scenario, and this run carries no registration setup
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/full-tests-final.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest starting inventory, registration dropped → host converges; take arbitration works | machine | **blocked (setup gap)** | No setup path exists to swallow a registration frame controllably; the fix's own capture is pinned by a gate, but the runtime claim needs its scenario |
| 2 | Craft product ids added after the first report → converge on the next re-report | machine | **blocked (setup gap)** | Same missing setup |
| 3 | Reconnect while in world → table rebuilt exactly once | machine | **blocked (reconnect run)** | Needs the session-special reconnect run |
| 4 | Guest picks up a world item (id already host-known) → no duplicate registration | machine | **pass** | The ticket's named runtime case runs in this run's green main suite (`4520/4520`) |
| 5 | Two guests with overlapping local counters → host keeps per-guest tables separate | machine | **pass** | Same suite run (the ticket's `TwoGuests_KeepSeparateTables`) |
| 6 | Item destroyed after registration → terminal fact wins, no resurrection | machine | **pass** | Same suite run (the ticket's `RegistrationOfADestroyedId_DoesNotResurrectTheEntry`) |
| 7 | Registration arrives before the item fact → accepted-first path still works | machine | **pass** | Same suite run (the ticket's `RegistrationBeforeTheItemFact_ArbitratesTheLaterReport`) |

## Limits
- Rows 1-3 are the reason the ticket stays open; rows 4-7 are recorded so the next run does not redo
  them. The next run needs a registration-drop scenario and the reconnect pass.
