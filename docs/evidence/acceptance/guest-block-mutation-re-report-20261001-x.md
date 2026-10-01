# Acceptance record — Guest world-block mutations have no periodic re-report

- Ticket: `guest-block-mutation-re-report` — verdict: **stays in `review/`** (rows 1–5, 8, 10 open; rows 6, 7, 9 unproven, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `s2-guest-state2.json`, `s2-alt-state2.json`, `s2-guest-rejoin.json`, `s2-alt-rejoin.json`, `s2-guest-rejoin2.json`, `s2-alt-rejoin2.json`, `s2-host-leave.json`, `s2-host-continue.json`, `s2-check-host.json`, `s2-check-guest.json`, `s2-check-alt.json`, `s2-host-itemsB-after2.json`, `s2-guest-itemsB-after.json`, `s2-alt-itemsB-after.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 6 | Guest reconnect: the host's table and the guest's world agree; mined blocks are not resurrected | machine | **unproven** | the guest's lobby rejoin did NOT re-activate its session (`role:Guest, active:false, inWorld:false` — `s2-guest-state2.json`; the same for the third client), and a clean relaunch that rejoined the same lobby stayed inactive as well (`s2-guest-rejoin2.json`): the recorded this-machine rejoin wedge reproduced. The world only came back together through the host's own leave→continue re-entry, after which the seven cells this batch mined all read air on all three clients (`s2-check-host/guest/alt.json`) — no resurrection — but the row's own "guest reconnect" path was not the mechanism, and one untouched control cell read `1` on the host vs `2` on both guests after the recovery (a stable divergence, recorded as an observation, not a row verdict here). |
| 7 | Layer regeneration: the table resets with the new baseline; no stale re-report | machine | **unproven** | not driven in this session — no `skiplayer` descent was run before the run closed. |
| 9 | Solo → lobby → guest joins: the accumulated difference is handed over exactly once | machine | **unproven** | the host was solo in the world while both guests sat in the lobby, and after the host's re-entry the mined cells arrived as air on both guests (same artifacts as row 6): the difference was delivered, but "exactly once" was not instrumented and the delivery path was the host's re-entry fan-out, not the row's own lobby-join edge. |
| 1, 2, 3, 4, 5, 8, 10 | swallowed report / first-writer after swallow / third-peer convergence / reconnect / layer / 65 536-entry cap / partial-damage-then-break | machine | **open** | the scenarios need a dropped report (or a 65 536-entry fill): this machine has no message-swallow injection (batch scope, capability boundary). Named open. |

## Limits

- The reconnect observations above are recorded as they happened, including the known wedge; they do not stand in for the open rows.
- One session is not a race proof.
