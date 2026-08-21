# Patch-contract same-name overload resolution — mechanism inventory and self-check

Owner cycle: backlog Tooling/item "Patch-contract same-name limitation: `PatchContractTests`
identifies targets by name, so a same-name overload pair cannot be distinguished (the
`LoadSceneAsync` case). Extend the contract only when a game update actually hits it."
Decision: close it now as a deliberate hardening of the game-update guard — the resolver
must never silently pick an arbitrary same-name overload. A constrained contract resolves by
its exact parameter types only; an unconstrained contract against a multi-overload target is
ambiguous and fails loudly with the fix instruction.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Contract facts already carry parameter types | `PatchContract.ParameterTypes` (PatchContract.cs:45-46) — the `[HarmonyPatch] argumentTypes` shape; empty means "any overload" |
| 2 | The test resolver had a name-only fallback | `PatchContractTests.Resolve` first tried `GetMethod(name, types)`, then fell through to `GetMethods().FirstOrDefault(m => m.Name == ...)` — a renamed/ret-typed overload could be checked against the WRONG same-name method |
| 3 | The runtime verifier had the same fallback | `PatchInventory.VerifyMissing`: `AccessTools.Method(declaring, methodName, argumentTypes) ?? AccessTools.Method(declaring, methodName)` (PatchInventory.cs:99-100) |
| 4 | The checker only compares the method handed to it | `PatchContractChecker.Check` validates parameter types against the method it receives (PatchContractChecker.cs:35-52); it cannot know that the resolver picked the wrong overload |
| 5 | A same-name overload pair is real in Unity/Game APIs | `SceneManager.LoadSceneAsync` has `(string)`, `(int)`, `(string, LoadSceneMode)`, `(int, LoadSceneMode)` overloads; the existing `SceneLoadPatches` already avoids this by declaring `[typeof(string)]` (SceneLoadPatches.cs:17) |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `PatchContractTests.Resolve` | Constrained contracts no longer fall back to name-only. Unconstrained contracts resolve only when exactly one method matches; multiple overloads throw `InvalidOperationException` naming the ambiguity and the fix |
| `PatchInventory.VerifyMissing` | Runtime parity: when `argumentTypes` are declared, a failed exact lookup no longer falls back to name-only — a game-update type change is reported as missing, not silently checked against a wrong overload. An unconstrained multi-overload target is reported as ambiguous instead of picking an arbitrary method |
| `PatchContractChecker` | Unchanged — it remains the pure verdict comparator; resolver correctness is the caller's responsibility |
| Existing patch declarations | Unchanged — no current contract needed new parameter types; the existing 15 `PatchContractTests` still resolve with the stricter rules |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Constrained overload selection | Exact parameter types choose the overload; no arbitrary fallback | `Resolve_ConstrainedContract_SelectsTheExactOverload` (fixture has `Overloaded(int)` + `Overloaded(string)`) |
| Unconstrained ambiguity | Fails loudly instead of silently picking one | `Resolve_UnconstrainedContractAgainstOverloads_ThrowsAmbiguous`; message contains "ambiguous" and "argumentTypes" |
| No masked mismatch | A constrained contract with a non-matching type returns null, then the checker reports not-found | `Resolve_ExactTypeMismatch_DoesNotFallBackToNameOnly` |
| Full guard regression | Every real patch contract still resolves | `EveryContract_ResolvesWithExactSignature` + full `dotnet test` (1028 passed) |
| Runtime parity | `VerifyMissing` skips the name-only fallback when argumentTypes are declared and reports an unconstrained multi-overload target as ambiguous | Static code path; `PatchContractChecker` is shared with the contract inventory tests |

## 4. Verification design

- **Unit tests:** three new resolver tests against a local fixture with intentionally
  overloaded methods (`Overloaded(int)` / `Overloaded(string)`).
- **Full regression:** `dotnet test CasualtiesUnknownOnline.slnx` — the stricter resolver
  runs against every real patch contract; the existing `LoadScene` / all other contracts
  still resolve.
- **Static evidence:** the fallback paths before and after are code-visible
  (`PatchContractTests.Resolve`, `PatchInventory.VerifyMissing`).
- **Runtime evidence:** development-period rule — L0/static evidence only, this is a
  test/tooling change with no wire or game-behavior surface; **no manual acceptance**
  (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-21)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore -m:1` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore --no-build` | 1028 passed / 0 failed (was 1025; +3 new resolver tests) |
| Focused `PatchContractTests` | 15 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean (workspace loaded with a warning; no file changes beyond the two touched source files) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | 26 files deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 7. Structure review

- `PatchContractTests.cs` remains a single top-level test class; the new fixtures and
  tests live inside the existing class (one file, one top-level type).
- `PatchInventory.VerifyMissing` was already under the 600-line gate (156 lines) and the
  change adds no class/state.
- No new expression-state bools; no dead mechanism. The old name-only fallback is removed
  from the constrained path, not left alongside it (AGENTS.md rule: delete stale mechanisms).
- No protocol or Game Adapter patch-surface change; the real game dir was still updated
  through deploy.ps1 so the installed `CasualtiesUnknownOnline.GameAdapter.dll` contains
  the tightened runtime verification, matching the repository.
