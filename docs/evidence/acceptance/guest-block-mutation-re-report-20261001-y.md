# Acceptance record — Guest block mutations: periodic re-report

- Ticket: `guest-block-mutation-re-report` — verdict: **stays in `review/`** (row 7 passes; rows 1–6, 8–10 open)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-census-after-*.json`, `y-skiplayer.json`, `y-census-layer2-*.json`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 7 | The host damages a cell, then the run descends a layer (`skiplayer`); the new baseline carries no stale damage from the old layer | machine | **pass** | immediately before the descent all three clients held the same 128 damaged rows (`y-census-after-*.json`); the host ran the game's own descent (`y-skiplayer.json`: `{"ok":true,"command":"skiplayer","args":0,"applied":true}`) and after the new layer loaded every client read an empty table — `tableCount=0` on the host at 22:25:15 and on all three at 22:25:49–52 (`y-census-layer2-*.json`); the guest's 22:25:16 read answered `no-world` while it was still re-entering the new layer, and the re-read answered 0. No stale damage survived the baseline move. |
| 1–5, 8, 10 | swallowed report / duplicate delivery / the 65 536-entry table | machine | **open** | swallowed-report family and an unreachable table fill (the batch scope's capability boundary, unchanged). |
| 6, 9 | rejoin delivery / solo→lobby→join exactly-once | machine | **open** | needs a session-reactivation check and a delivery counter the run does not have. |

## Limits

- The descent is the game's own debug entry (`game-console command=skiplayer`), not a natural descent; it
  is the same generation boundary a natural descent crosses (batch `20261001-m` established the entry).
- One descent is one sample; the row's claim is about the new baseline carrying nothing, which the three
  post-descent reads (all zero) support.
