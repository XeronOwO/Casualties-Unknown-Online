# Acceptance scenario recipe layer — self-check

- Cycle: 2026-09-27 (tools/acceptance) — the batch `20260927-c` finding: the committed driver's closed
  vocabulary reaches the Online UI's session surface but no gameplay state, so the 45 session-class
  tickets stayed in `review/` with a setup gap and a per-scenario recipe named as the missing half
  (`docs/evidence/acceptance/20260927-c-scope.md`).
- Scope: `tools/acceptance/drive-in-process.ps1` (`-Action recipe`), `tools/acceptance/recipes/`, the
  normative gate and the black-box tests. No `src/` change: a recipe evaluates inside the running client
  and the game's own validation decides (the host admits a carry, the body simulates its own state).

## Mechanism inventory

| Mechanism | Evidence (source or runtime) | What the recipe layer does with it |
|---|---|---|
| Carry admission | `src/CasualtiesUnknownOnline.Plugin/OnlineUiActions.cs` calls `IPlayerInteractionControl.SendCarryStartRequest` / `SendPiggybackRequest` / `SendCarryOnBackRequest` / `SendCarryStopRequest`; the host validates in `Runtime/Session/PlayerInteraction/PlayerCarryService.cs` | `carry-start` / `carry-stop` call the same four entries, so the host's visibility and health validation and the kernel projection decide |
| Local body state | the game's `Body` public fields (`consciousness`, `brainHealth`, `energy`, `badSleepAmount`, `idleTime`, `standing`, `sleeping`, `crouching`, `isRight`, `moveDir`, `rb`) and its derived `alive` / `conscious` | `body-force` / `body-read` write and read exactly those fields |
| Movement | the game's `PlayerCamera` rewrites `body.moveDir` from the keyboard every frame | `move-drive` writes the intent once per invocation (the horizontal rigidbody velocity too in `mode=slide`, never the vertical one); the run loops the invocation for the observation window |
| Carry readings | the remote clones' `RemoteBodyDriver` fields (`LimbSeparationWindowMax`, `PinDriftWindowMax`, `PinCountInWindow`, `PinnedCarrierSteamId`, `PinnedToLocalCarrier`, `IsCarriedRider`, `IsCarrier`, `RagdollPoseActive`, `LegSpeedMult`) and the local body's `CarriedBodyDriver.CarrierSteamId`; the two tokens the 1 Hz line names are derived on the spot — `pinned-to-carrier` from `PinCountInWindow > 0`, `mounted-to-local-carrier` from the clone root's parent chain reaching `CUO_CarryMount` | `carry-read` reads them by reflection — the driver types are internal to the adapter and a snippet cannot reference them |
| Evaluator language | the HotRepl Mono.CSharp evaluator (local clone) references every assembly already in the AppDomain (minus its stdlib filter) and compiles C# 7.x | recipes may use game types directly; the normative gate keeps every recipe C# 7.x and argument-declared. Nested helper lambdas stay capture-free by the shipped pattern: the `BindingFlags` they read is a `const`, which the compiler inlines — the same shape `driver/InProcessDriver.cs` has used in the live client |

## Self-check table

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | Driver action vocabulary | `-Action recipe -Recipe <name> -RecipeArg k=v`: one eval per invocation, the recipe's JSON report returned, bad recipe/arguments refused before the socket opens | `DriverToolTests.Recipe_RunsOneCommittedRecipeAsOneEval`, `Recipe_ReportThatFailsIsADriverFailure`, `Recipe_ReportWithoutAnOkFieldIsADriverFailure`, `Recipe_WithAnEvalErrorFails` |
| 2 | Argument contract | `{{s:key}}` / `{{n:key}}` substituted exactly once; a missing, duplicated or unused argument is a usage error | `DriverToolTests.Recipe_MissingUnusedOrUnknownArgumentsAreUsageErrors` |
| 3 | Recipe language | every `recipes/*.cs` stays inside the evaluator's language and declares exactly the arguments it uses | `AcceptanceDriverGateTests.TheRecipesStayInTheEvaluatorsLanguageAndDeclareTheirArguments` (census floor plus positive/negative samples) |
| 4 | OS-input boundary | the recipe layer adds no OS-level input; the directory scan now covers it | `AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput` |
| 5 | Capability truth | preflight, workflow and dependency pages name the recipe layer as part of `input` | `preflight.ps1` input row; `docs/acceptance/workflow.md` §5 and the capability table; `docs/acceptance/dependencies.md` input row |

## Limits (what this cycle does not claim)

- The recipes are proven against a running client: the 2026-09-27 smoke run (session-d) exercised all six
  on deployed artifact `0.1.0+631a8d82` — a piggyback relation, `carry-read` on both views with
  `mountedToLocalCarrier`/`pinnedToCarrier` true and both drift readings zero, a 25-call movement window
  that moved the carrier 15 units, a release, and a forced-state write/read-back. What remains is the
  per-ticket acceptance run (batch d): its rows, not this layer, are unproven.
- The recipes cover the carry / forced-body-state family; the remaining session families (hit and
  visibility, sounds, save and reconnect, Online UI) still need their own recipes.
- `move-drive` is one frame's nudge. Whether the frame order lets the game's own physics walk the body
  between the write and the next `PlayerCamera` pass is settled in that run, not here.
