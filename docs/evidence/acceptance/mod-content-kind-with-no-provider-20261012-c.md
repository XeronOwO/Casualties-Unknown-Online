# Acceptance record — Content kinds with no provider: the vocabulary names what binds, the binder answers for the rest

- Ticket: `mod-content-kind-with-no-provider` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

The rows below are the ticket's own acceptance table, in its order. Every row is load-time and client-local and
is judged by the suites and gates this run re-ran, or by reading the committed vocabulary and baseline.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A registration under a kind no provider binds is reported at load time, naming the kind and the definition, instead of passing in silence | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModContentBinderTests.UnknownKind_IsReportedAsNeverMaterialized` — `Passed` in this run; it drives the production binder with a recording logger and reads the warning's own text |
| 2 | The framework's content-kind vocabulary names exactly the kinds a provider claims, one name per kind, and every provider is named by a registration statement | machine | pass | `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.ContentKindProviderCoverageGateTests.TheVocabulary_NamesExactlyTheKindsTheProvidersDeclare` — `Passed` in this run; the row's second half is the same gate's `...EveryProvider_IsNamedByARegistrationStatement`, also `Passed` |
| 3 | A mod can no longer name the three removed kinds through the API | machine | pass | `file:docs/contracts/abstractions-api-baseline.txt` — its tombstone for `CasualtiesUnknownOnline.Abstractions.ModContentKind.Entity` reads "no CUO provider binds it: the kind word belongs to the built-in cu:player resource entry, and custom enemies stay a parked capability" (its siblings on the two following lines carry `Locale` and `Setting`), each with its own reason. The vocabulary read in this run holds exactly the nine bound kinds and neither name: `src/CasualtiesUnknownOnline.Abstractions/ModContentKind.cs` — "The content kinds CUO binds: one constant per kind a content provider in the" |
| 4 | The nine bound kinds are unchanged: each still has its provider, its kind word and its materialization path | machine | pass | Three halves, all `Passed` in this run: the KIND WORD is the vocabulary ↔ provider census, `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.ContentKindProviderCoverageGateTests.TheVocabulary_NamesExactlyTheKindsTheProvidersDeclare`; the PROVIDER half is the same gate's `...EveryProvider_IsNamedByARegistrationStatement`; and the MATERIALIZATION paths are driven by the nine providers' own suites, which this run's outcome index carries green — `CasualtiesUnknownOnline.Tests.Patching.ItemWorldGenProviderTests.TryBind_AcceptsValidWorldSpawnAndRejectsInvalidValues` is one named example. The gate reads source, so the census proves the correspondence rather than a running container executing it; the `Limits` below states that consequence rather than hiding it |
| 5 | The console's built-in `cu:player` entry keeps its kind word and its display name | machine | pass | `CasualtiesUnknownOnline.Tests.Content.BuiltInResourceLocationSourceTests.Entries_CarryThePlayerId_WithTheEntityKindAndTheLocalizedName` — `Passed` in this run; the row's second half is the same suite's `...Entries_TakeTheDisplayNameFromTheLocalizationCatalogue`, also `Passed`, and the word now lives in the source's own home: `src/CasualtiesUnknownOnline.Runtime/Session/Content/BuiltInResourceLocationSource.cs` — `private const string PlayerKind = "entity";` |

## Residuals for the user

None. Every row is decided by this run's own test outcomes or by inspection of the committed tree; no row
asks for a person's judgement. The ticket's own `Limits` name one surface a person verifies on the physical
machine (the Online UI row and the config entry of the sibling parity rule) — that belongs to
`mod-native-binding-handshake-parity`, not to this ticket, and no row here depends on it.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process
  probe. Row 1 is a load-time behaviour judged by the suite against a recording logger, as the ticket states;
  no client's log file was read.
- **The ticket's table records its rows as `Verified 2026-10-07`.** That date is a state of the cycle that
  wrote the ticket, and it was not re-measured; the durable outcome was — the gate, the binder case and the
  console-entry cases the rows name still exist and are `Passed` in this run. The earlier cycle's recorded red
  ("red before the change") was likewise not re-run.
- **The compile-time half of row 3 stays a mod-side fact this repository cannot execute**, as the ticket's own
  `Limits` say: what is asserted here is that the vocabulary no longer declares the three names and that the
  three tombstones record why — not that a third-party mod fails to build.
- **The gate reads source, not the built container** (the test host composes the Runtime without the Game
  Adapter), so the nine-way census proves that a registration statement names each provider rather than that a
  running container executes it — the ticket names that consequence itself, and this run does not upgrade it.
- The binder answers once, at its single load-time pass, and a resource entry's kind word stays display text
  rather than a sealed vocabulary; neither is claimed here as anything more.
