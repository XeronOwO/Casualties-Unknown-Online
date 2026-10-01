# Acceptance record — Guest break drops are lost when the break report is swallowed

- Ticket: `guest-break-drops-recovery` — verdict: **stays in `review/`** (rows 5 and 6 pass; rows 1–4 and 7 stay open, named below)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `s1-r3-race.json`, `s1-r3-alt-read.json`, `s1-r4-race.json`, `s1-r4-host-log.txt`, `s1-r4-guest-log.txt`, `s1-r5-alt-log.txt`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 5 | Another sender's break of the same cell refuses the loser's drops and rolls them back on the breaker | machine | **pass** | in the race rounds R3/R4 the host refused the second sender's break report and every drop went back: `[BlockBreak] 76561199526807662's break report refused (the cell's break belongs to another writer) — 1 block drop(s) + 0 building drop(s) rejected` (`s1-r4-host-log.txt`), and the breaker destroyed its own copy: `[ItemReject] block drop 14451443822 (scrapmetal) refused — destroying the local copy` (`s1-r4-guest-log.txt`); the R3 round shows the same shape with `10156476526 (plasticchunk)`. |
| 6 | A third party sees the same drop identities and no duplicate materialization | machine | **pass** | the third client claims the winner's drops by item ids IDENTICAL to the host's registrations: `s1-r5-alt-log.txt:6` `op=13 item=1198616856515` (21:04:16.940) against the host's `s1-r4-host-log.txt:4` `op=8 item=1198616856515 … Committed events=[DropCaptured]` (21:04:16.872), and `s1-r5-alt-log.txt:8` `op=15 item=1202911823811` (21:05:13.816) against the host's `s1-r4-host-log.txt:9` `op=11 item=1202911823811` (21:05:13.734). One claim per id; the `Skipped [AlreadySynced]` verdict is the sync path refusing to create a second copy. |
| 1, 2, 3, 4, 7 | swallowed report / lost air write / duplicate re-report / world-reset clears pending | machine | **open** | the scenario needs a dropped report: this machine has no message-swallow injection (batch scope, capability boundary). Named open, not replaced by a weaker check. |
| 2b | A payload-free report is never relayed (the host-side premise) | static | **not a live row** | pinned by the landing record's own test; this run did not drive it. |

## Limits

- Rows 5–6 are judged from the two-sender race this run staged for `block-break-first-writer-wins`; the ticket's own swallow-driven rows remain open.
- One session is not a race proof; each race round is one timing sample.
