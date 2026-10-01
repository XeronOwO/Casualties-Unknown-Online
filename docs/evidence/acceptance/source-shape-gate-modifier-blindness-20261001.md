# Acceptance record — The one-top-level-type gate sees every modifier

- Ticket: `source-shape-gate-modifier-blindness` — verdict: moved to `done/` (the batch `20260927-b`
  rejection is closed)
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47 (31 main-suite and
  48 gate tests, all passed); full suite with build (4,573 + 300, 0 failed); `dotnet format` exit 0
- Dependencies: `dotnet` (build, both suites), `format`, repository inspection; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` — `focused-20261001-t.log`,
  `full-suite-20261001-t.log`, `format-20261001-t.log`, `verify-deploy.txt`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The matcher accepts every modifier the language allows on a top-level type declaration, in any order — a fact-based matcher | machine | pass | `SourceShapeGateTests.TheMatcher_SeesEveryDeclarationShapeAndIgnoresMentions` `Passed` in this run's gate suite (300/300); the theory's 29 shape samples include `public`, `internal`, `private`, `protected`, `file`, `sealed`, `abstract`, `static`, `readonly`, `partial`, `unsafe`, `new` and `ref` shapes |
| 2 | The matcher's own samples pin the shapes — a positive sample per modifier and negatives for a doc-comment mention and a nested type | machine | pass | `SourceShapeGateTests.cs` carries 29 shape samples (19 positive, 10 negative) plus the 6 depth/name samples; the batch-`20260927-b` rejection's missing positive is at `[InlineData("public new class Foo", true)]`, and the theory `Passed` on this run |
| 3 | `record struct` / `record class` are read as the two-word keyword, so the NAME is the name; the failure text names the declarations found and states the scope | machine | pass | `OnlyADepthZeroDeclarationCounts_AndTheNameFollowsTheKeyword` `Passed` (`Point`/`Node` cases); source read of `TryReadTopLevelTypeName` and of the failure text "N top-level type declarations — … (rule: one top-level type per file; a nested declaration belongs to its owner and does not count)" |
| 4 | The pre-existing multi-type files are split one per file; census floors keep the scan from passing by finding nothing | machine | pass | all 15 files of the seven splits resolve under `src/CasualtiesUnknownOnline.Runtime/Session/`; `Architecture_OneTopLevelTypePerFileAndAggregateLimits` `Passed` in the 300/300 gate suite; source read of the 1,000-file / 1,000-declaration floors |
| 5 | Declared limits: a line-wrapped declaration and a top-level `delegate` stay outside the matcher; the brace counter is string-blind | machine | pass | the `public delegate void Handler(int x);` negative sample `Passed`; the matcher reads one trimmed line at a time; the limits are the ticket's own record |
| 6 | The three-check acceptance for a gate change: the old revision fails the widened gate, the fixed tree passes it, and the repository has no false positives | machine | pass, with a named boundary | the fixed tree passes (300/300) and the aggregate scan reports no false positive over `src/`; the old-revision leg is the cycle's recorded red in `docs/evidence/selfchecks/tooling/one-top-level-type-gate-selfcheck.md`, which exists in the tree — this batch re-establishes only the fixed-tree and no-false-positive legs |

## Residuals for the user

None.

## Limits

- No client was started; the ticket changes no runtime behaviour.
- Sample counts are read from the source (a theory reports one outcome, not one per `InlineData` case);
  the gate suite's per-test outcome is `Passed` for both theories.
- The pre-change red leg is not re-executed against the old revision: it is the named selfcheck's
  point-in-time record, and the deciding new sample plus the fixed-tree pass are this run's evidence.
