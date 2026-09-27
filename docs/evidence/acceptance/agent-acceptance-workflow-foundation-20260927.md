# Acceptance record — Agent acceptance workflow — foundation

- Ticket: `agent-acceptance-workflow-foundation` — verdict: moved to `done/`
- Batch: `20260927-a` — one ticket. The ~130 tickets already in `review/` are **not** in this batch:
  their rows need the two-client session (stage 2), so they stay in `review/` as `blocked` on that
  capability rather than being passed.
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `cb9940f1`.
- Deployed artifact: not applicable — the ticket changes no runtime code. The preflight reported the
  deployed plugin identity (`CasualtiesUnknownOnline.dll`, `0.1.0+fdd72c842696fb7710924c856254cd9c98c08d44`)
  and deployment freshness compared with repository `HEAD`.
- Run: 2026-09-27 — dependency preflight, the gate project, the full suite, and an independent
  adversarial review (fresh context, frozen tree). No game session.
- Dependencies used: `dotnet`, `game`, `deploy`, `steam`, `hotrepl`, `capture`, `logs`, `artifacts`
  (`present`); `input` (`pending`, staged) — preflight exit code `0`.
- Artifacts: none — every row is a text verdict; no frame, recording or probe dump was needed.

| # | Row (the ticket's Stage 1 criteria) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The preflight runs on this machine: one row per dependency, a summary line, an exit code that matches the missing set | machine | pass | run output: `9 present, 1 pending`, `RESULT: OK - a full two-client run is possible`, exit `0` |
| 2 | A required machine fact is absent → `FACT-MISSING`, exit code 2, the user is asked | machine | pass | `-FactsPath` pointed at a file without the section: `14 missing`, `RESULT: MISSING - game, steam, sandboxie, logs, artifacts`, exit `2` |
| 3 | The preflight runs twice: same report, nothing on the machine written | machine | pass | two runs with identical rows; read-only verb audit of the whole script (review report §B.3) |
| 4 | No absolute machine path in the change | machine | pass | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` green; the same scan over 3,068 candidate files returned 0 hits (review §B.13) |
| 5 | The instruction budget is measured | machine | pass | gates 288/288: always-loaded pair 58,973 ≤ 65,536 bytes, every nested instruction file ≤ 5,120, both new area files asserted as discovered |
| 6 | The dependency table and the script agree id by id | machine | pass | the two id sets reconcile in both directions (review §B.4), and the Detection column now states what each check really decides |
| 7 | Batching and the lessons pass are binding, and `lessons.md` holds this cycle's lessons | machine | pass | `docs/acceptance/AGENTS.md` rules 2 and 9; `workflow.md` §1 and §10; `lessons.md` carries four entries |
| 8 | `sandboxie-exe` unset while `SbieSvc` runs → the program is derived, the row is `present` | machine | pass | run output: `program (sandboxie-exe), guest sandbox root, SbieSvc and SbieDrv resolve` (the key is also filled in the local facts) |

## Residuals for the user

None. Every row of this ticket is a process/tooling fact that the run could decide from evidence it
collected itself, so nothing was pushed back as a judgement call.

## Limits

- No game session was run: this ticket changes no runtime behaviour, and none of its rows needs a
  two-client session. The session step is stage 2 of the ticket's roadmap.
- The adversarial check for this record is the independent review (fresh context, read-only, frozen
  tree). It found 3 major and 7 minor findings — the false `missing` for Sandboxie, the over-claimed
  detection column, the over-claimed rule text, and smaller defects — all fixed before this record was
  written, so the verdicts above describe the post-fix tree.
- The review also left one open runtime question it could not settle read-only: whether a nested
  `AGENTS.local.md` is injected under **every** session in this workspace or only while working in its
  area. `lessons.md` records the measured numbers either way; the loader's exact accounting is for the
  next cycle.
