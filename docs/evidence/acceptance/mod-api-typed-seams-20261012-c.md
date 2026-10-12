# Acceptance record — Typed seams: no `object` handle and no string-keyed call in the mod contract

- Ticket: `mod-api-typed-seams` — verdict: **moved to `done/`**
- Batch: 20261012-c — offline batch; scope and plan: docs/evidence/acceptance/20261012-c-scope.md
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: CasualtiesUnknownOnline.dll 0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91 — deployed and hash-verified in this run (tools/verify-deploy.ps1 exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (exit 0; its own last line reads `RESULT: OK - a full two-client run is possible`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`, `swap-needed=false`), both captured into the batch directory before the build; no client capability (steam, sandboxie, sandbox-alt, hotrepl, capture, input, logs) is consumed by this run
- Artifacts: none — the run's logs and TRX results are under the directory named by acceptance-artifacts-dir

This ticket has no acceptance table: it states its landed surface as a `What landed` list. The six rows below
are written from those six bullets, in their order, before judging them; each row text is the bullet's own
claim.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `IModNativeApi` exposes `CanAccess`, `CanInvoke(string)` and `TryGetLocalPlayerState(out IModNativeLocalPlayerState)` — there is no untyped way in — and the two removals are tombstoned in the contract baseline | machine | pass | `file:src/CasualtiesUnknownOnline.Abstractions/IModNativeApi.cs` — "There is no untyped way in", with the interface read in this run declaring exactly those three members. The baseline carries the removals: its tombstone for `CasualtiesUnknownOnline.Abstractions.IModNativeApi.TryInvoke(string operation, object?[] arguments, out object? result)` reads "the mod-visible contract carries no erased type: each registered operation is reached through its own typed projection (TryGetLocalPlayerState), and CanInvoke stays as the availability probe" |
| 2 | `IModNativeApiProvider` exposes `IsRegistered(string)` plus `TryGetLocalPlayerState(out …)`, and both the disabled provider and the Game Adapter's implementation follow the seam | machine | pass | `CasualtiesUnknownOnline.Tests.Patching.GameAdapterNativeApiContractTests.GameAdapter_ImplementsModNativeApiProvider` — `Passed` in this run (the Game Adapter's implementation); the disabled half is read in the committed tree: `src/CasualtiesUnknownOnline.Runtime/Session/Mods/DisabledModNativeApiProvider.cs` — `internal sealed class DisabledModNativeApiProvider : IModNativeApiProvider`. The refusing-provider path is driven by `CasualtiesUnknownOnline.Tests.Mods.ModNativeApiTests.ProviderThatRefusesEveryOperation_IsRefused`, `Passed` |
| 3 | `ModNativeApiPolicy` is the operation-id shape rail only: its argument/result value surface, its four caps and its rank-and-element-type array rule are deleted rather than moved | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNativeApiTests.PolicyRails_AreExact` — `Passed` in this run, with `...MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` also `Passed`; the file read in this run is 39 lines and says of itself `the operation-id shape rail, and nothing else` (`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModNativeApiPolicy.cs`) |
| 4 | `IStartingSupplyBehaviour` sits in the runtime's world namespace instead of the mod-visible Abstractions surface | machine | pass | `file:src/CasualtiesUnknownOnline.Runtime/Session/World/IStartingSupplyBehaviour.cs` — `namespace CasualtiesUnknownOnline.Runtime.Session.World;`, with the file's own doc calling it "a FRAMEWORK seam, not a mod contract". Its removal from the mod surface is recorded: the baseline's tombstone for the type `CasualtiesUnknownOnline.Abstractions.IStartingSupplyBehaviour` reads "moved to CasualtiesUnknownOnline.Runtime.Session.World: the mod-visible surface promises a shape to mod authors and may not carry a type no mod can reach" |
| 5 | `ModNativeApiTests` covers the reworked cases (the permission gate, the typed projection, no-body, an all-refusing provider, an unregistered operation, malformed ids and the policy rails) and the fake provider backs them | machine | pass | `CasualtiesUnknownOnline.Tests.Mods.ModNativeApiTests.WithPermission_ReturnsTheProvidersTypedProjection` — `Passed` in this run; the run's outcome index carries exactly the seven cases the ticket names — `MissingAccessNativeApiPermission_IsRefused`, `NoLocalBodyToProject_IsRefused`, `ProviderThatRefusesEveryOperation_IsRefused`, `UnregisteredOperation_IsNotInvokable`, `MalformedOperationId_IsRefusedBeforeTheProviderSeesIt` and `PolicyRails_AreExact` — all `Passed` |
| 6 | The native-operations section of both documentation blocks documents the typed seam — the sample no longer calls `TryInvoke` — and the contract baseline records the removals as five tombstones | machine | pass | `file:docs/en/reference/mod-api.md` — "There is no untyped way in: an operation's result is declared by the signature", in a section whose sample reaches the operation through `CanInvoke` + `TryGetLocalPlayerState`; the Chinese pair carries the same sample. Neither page contains `TryInvoke` in this run's read of them, and the baseline's five tombstones are the four `IStartingSupplyBehaviour` lines plus the `IModNativeApi.TryInvoke` line quoted in row 1 |
| 7 | The sweep's own disposition table keeps `IModCommands.TryExecute(string name, IReadOnlyList<string> arguments, …)`: the console is the mod's own command surface, and the command is recorded as checked rather than retyped | machine | pass | `file:docs/backlog/done/mod-api-typed-seams.md` — "KEPT, and recorded as checked: this is a console, and the command is the mod's own — a name and its text arguments are its natural shape"; the contract baseline carries the member as a live `Stable` line rather than a removal — `docs/contracts/abstractions-api-baseline.txt` — "member|Stable|CasualtiesUnknownOnline.Abstractions.IModCommands.TryExecute(string name, IReadOnlyList<string> arguments, Action<IModCommandResult> callback)|method|-> bool" |

## Residuals for the user

None. Every row is decided by this run's own test outcomes or by inspection of the committed tree; no row
asks for a person's judgement.

## Limits

- **No client was started**, so this run produced no rendering, no frame, no live log and no in-process probe.
  Every row is decided by test output from this run or by reading the committed source, page and baseline. The
  ticket's own `Limits` say the acceptance batch re-runs the mod-API rows against deployed DLLs as usual — that
  is a session row this offline batch does not claim and does not run.
- **The bullets' record pointers are not claimed.** Two of the six bullets name a decision entry
  (`decision 250`) and the ticket names decision 249/250 in its body; the rows above anchor the page and the
  contract baseline instead, and no decision entry is claimed as verified here.
- **The ticket's historical measurements were not re-measured**: the policy's `133 → 39 lines` (only the
  current 39-line file was read), the "only two `object` signatures left on the whole contract" sweep, and
  `GameAdapter.cs`'s line count are this cycle's own numbers. The durable outcome was judged instead — the
  policy file, the typed members, the tombstones and the suites the rows name all exist and are green in this
  run.
- The one production implementation of the native operation seam is covered reflectively
  (`GameAdapterNativeApiContractTests`), as the ticket states; nothing here observes a live game call.
