# Acceptance record — Global unified projection framework

- Ticket: `global-projection-framework` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket carries an explicit "Acceptance criteria" list; each of its five bullets is one row below.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Every rebuildable projection domain registers through the global contract, while the named write-side/native-to-kernel adapters, stateless restore appliers and one-shot event fan-outs are explicitly outside the inventory | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.ProjectionHealthCoordinatorTests.Register_TypedDomain_GoesThroughGlobalContract` — `Passed` (registration goes through the typed domain contract); `docs/architecture/projection-framework.md`, read in this run, records the same boundary under "Audited boundaries:" (the enemy buffer is not a rebuildable read model, and `EnemyKernelProjection` / `EnemyKernelRestoreProjection` / `EnemyCombatKernelProjection` are named as kernel projections, a restore applier and a one-shot fan-out), while the ticket's own "Remaining scope / audited boundary" names the full class list, including `PlayerKernelStatusProjection` and the fluid host-side input path |
| 2 | A single `Snapshot()` lists all registered domains with their revision, dirty, degraded and error state | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.GlobalProjectionFrameworkTests.Snapshot_ContainsRunCarryAndRemotePresentationDomains` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.ProjectionHealthCoordinatorTests.Run_Success_TracksLastSuccessfulRevision` — `Passed` (the revision half of the same surface) |
| 3 | A domain failure is contained and rebuilt on the main-thread pump, without reverting the authoritative source | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.ProjectionHealthCoordinatorTests.Pump_RebuildsDirtyDomainFromKernelReadModelAndClearsDirty` — `Passed`; `CasualtiesUnknownOnline.Tests.Session.GlobalProjectionFrameworkTests.ModStatus_RegisteredDomain_RecoversFromInjectedFailureAndKeepsReadModel` — `Passed` (the injected-failure recovery is green for the `run`, `players-carry` and `remote-character-presentation` domains in the same run) |
| 4 | No new projection domain can be added without a typed read model and a registered rebuild path | `machine` | **pass** | `CasualtiesUnknownOnline.Tests.Session.ProjectionHealthCoordinatorTests.Register_TypedNullDomain_Throws` — `Passed` (the typed entry point refuses a domain that is not a usable read model); `docs/architecture/projection-framework.md` rule 5 — "it must either reuse an existing typed read model or create one that is registered under the global contract" |
| 5 | Full build, format, normative gates and full tests pass, and an independent adversarial review ran before the ticket moved to `review/` | `machine` | **pass** | This run's own per-case index, `test-outcomes.txt`, holds 5681 cases and every one is `Passed` (no failed, no skipped case), with the gate suite at 626 and the behavioural suite at 5072, both exit 0, plus a clean build (0 warnings / 0 errors) and a format run after which the working tree was byte-identical; the review half is read through its durable outcome — `CasualtiesUnknownOnline.Tests.Session.GlobalProjectionFrameworkTests.ModStatus_GuestHidesHostAuthoritativeProjectionSnapshots` — `Passed`, the regression case for the host-authoritative presence leak the ticket says the second review found |

## Residuals for the user

None for this run: every row is a machine read of this run's own outputs or of the committed tree, and no
row asks for a person's judgement. The ticket's own closing note stands — a real dual-client visual pass
remains the user's final unified acceptance, which this offline batch neither performs nor substitutes for.

## Limits

- **No client was started.** No rendering, frame, live log or probe was observed; the containment and
  rebuild rows are decided by this run's suite output, and the inventory and boundary rows by inspection of
  the committed architecture page and the typed registration test.
- **The historical suite numbers were not re-measured.** The ticket states "2516 `CasualtiesUnknownOnline.Tests`
  + 17 normative gates"; this run measured 5072 behavioural cases and 626 normative-gate cases, both
  0 failed / 0 skipped (`full.log`, `gates.log`). The 2516 and 17 figures are the writing cycle's
  measurements.
- **The three adversarial reviews are a process record, not a row here.** This batch did not re-run them;
  what row 5 reads is the regression case one of their findings produced.
- **Row 1's inventory is read, not re-derived.** The page's "implemented now" list and its audited
  boundaries are the committed contract for what registers and what deliberately does not; this run did not
  re-audit every projection class in the tree.
- **Row 4 is a contract-level check.** The typed entry point and the page's rule are the evidence; the run
  does not exhibit a rejected attempt to register an untyped domain beyond the null-domain case.
