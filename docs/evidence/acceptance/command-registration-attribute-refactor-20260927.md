# Acceptance record — Command registration Attribute/reflection refactor

- Ticket: `command-registration-attribute-refactor` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket carries no acceptance section; its rows are the claims of its `Landed` section.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Built-ins are discovered via `[ConsoleCommand]`-marked methods into `ConsoleCommandRegistry`; the hard-coded `RegisterBuiltIns()` list is gone. | machine | pass | `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ConsoleCommandRegistry.cs` and `ConsoleCommandAttribute.cs` exist and the registry scans the command owner's type for the attribute; a bounded grep for `RegisterBuiltIns` over `src/`, `tests/` and `tools/` returns zero hits (the only three repository mentions are two lines of the ticket itself and one selfcheck line); `ModConsoleCommandTests` 10 passed and the console suites stayed green in the main suite 4482/4482 |
| 2 | Abstractions exposes `IModConsoleCommands` / `ModConsoleCommand` through the ConsoleCommands property on IModContext for local mod console commands (no wire relay). | machine | pass | `src/CasualtiesUnknownOnline.Abstractions/IModConsoleCommands.cs`, `ModConsoleCommand.cs` and `IModContext.cs` are in the tree and the context exposes the console-command surface; the wire stays untouched — a bounded grep for `ConsoleCommand` across `src/CasualtiesUnknownOnline.Protocol` returns zero hits; the local-execution contract is `ModConsoleCommandTests.ModConsoleCommand_ExecutesLocallyAndAppearsInCompletion` and `ModConsoleCommandTests.ModConsoleCommand_ResourceLocation_ExecutesLocally`, both passed; the guest-side absence is decided by the zero-hit `ConsoleCommand` grep over the protocol project, not by the test |
| 3 | `CommandConsoleService`, the registry, the mod adapter and the command context are separate responsibility units; architecture gates pass. | machine | pass | each is its own file and top-level type under `src/CasualtiesUnknownOnline.Runtime/Session/` (`Commands/CommandConsoleService.cs`, `Commands/ConsoleCommandRegistry.cs`, `Mods/ModConsoleCommandAdapter.cs`, `Mods/ModContext.cs`); the architecture gate `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` passed in the gate suite 288/288; `Commands/CommandConsoleService.cs` measures 584 lines, under the 600-line cap |

## Residuals for the user
None.

## Limits
No client was started; nothing in this ticket is observable only in a running game, and no rendered console surface was exercised. Rows 1 and 3 are decided by file inspection (type/file layout, the zero-hit greps, the measured line count) plus the gate and suite outcomes; row 2's no-wire half is a zero-hit grep over the protocol project. The ticket's Constraints are covered by the same evidence (existing command behaviour and the contract tests green, no wire change, the line-count cap held), and its selfcheck sheet `docs/evidence/selfchecks/ui/command-console-selfcheck.md` exists. The ticket's own cycle counts are not re-used here.
