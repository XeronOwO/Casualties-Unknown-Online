# Acceptance record — Composition root: feature modules and a unified session-reset lifecycle

- Ticket: `composition-root-feature-modules` — verdict: moved to `done/`
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47
  (`SessionLifecycleGateTests` 11/11, `CuoServiceOrderTests` 3/3); this run's registration comparison
  over `26632345^` / `26632345`; full suite with build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the repository's own history (`git show 26632345^` /
  `26632345`), repository inspection; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` — `compose-compare.py`,
  `focused-20261001-t.log`, `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A new session-scoped singleton that does not implement the reset contract fails the gate | machine | pass | `SessionLifecycleGateTests` 11/11 `Passed`; the gate carries the contract-shape matcher with positive and negative samples (`ISessionReset` declaration, retired spellings) |
| 2 | A synthetic missed unsubscribe fails the gate | machine | pass | the same 11/11; the gate pins every session-lifecycle subscription's unbind half in the same file and carries a missed-unbind negative sample |
| 3 | The composition behaviour is unchanged: the same services exist with the same lifetimes, proven by the existing DI and startup suites | machine + re-derived comparison | pass | the run compared `26632345^` with `26632345` over the Runtime tree: registrations moved from 3 files/171 calls to 14 files/171 calls with an **identical (lifetime, service-type) multiset** (0 only-in-old, 0 only-in-new); `CuoServiceOrderTests` 3/3 `Passed` (the 18-name `ICuoService` update order, the resource-source order and handler discovery); `SessionLifecycleGateTests` 11/11 |
| 4 | `CuoBootstrap` comes off the 600-line watchlist because registrations moved, not because the file was split by formatting | machine | pass | this run reads `CuoBootstrap.cs` at 158 lines; `docs/backlog/watchlist/architecture-watchlist.md` carries no `CuoBootstrap` entry |

## Residuals for the user

None.

## Limits

- Row 3's lifetime half is a source-level comparison of the registration calls (the trees before and
  after the split), not a runtime descriptor dump; the behavioural half is the order/lifecycle gate and
  the suites, which pass. The equality is over service type and lifetime, not over factory bodies —
  those were compared as part of the same call multiset text, and no call disappeared.
- No runtime behaviour is observed: the ticket's own "Not verified here" states the change is
  architectural and this row set is its machine half.
