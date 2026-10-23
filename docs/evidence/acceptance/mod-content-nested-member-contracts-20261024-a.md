# Acceptance — a mod-authored nested implementation materializes, and a null nested member is not carried

- Ticket: `docs/backlog/done/mod-content-nested-member-contracts.md`
- Artifact: the deployed client `0.1.0+a56e5d57` (the framework under test); the EXAMPLE MOD DLL was built from
  this cycle's change and loaded as the fixture
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `nst`)
- Date: 2026-10-24
- Result: **all three rows pass.**

## What was added to the example mod

`ExampleComputedTool` — the mod's OWN `IModItemTool` implementation, with `StaminaUse = 30`, `Damage = 1000`
and the rest of the contract's values. It is used twice, which is exactly the ticket's two registration shapes:

- **By attribute**: `ExampleCleaver` (`[ModContent]`, an `IModItemDefinition` class) returns the tool from its
  `Tool` member; no registration call.
- **By code**: the code-registered `example.trophy` sets `ModItemDefinition.Tool = ExampleComputedTool.Instance`
  — a framework data class carrying the mod's own nested implementation.

## Rows

| Row | Verdict |
|---|---|
| A mod-authored nested implementation materializes with that tool's values | pass — the attribute-declared `example.cleaver` was created by the native grant and its own `useAction` ran: stamina `100 → 70` (delta **-30**, the tool's own value) |
| The same declaration through code, a framework data class with a nested implementation, still materializes | pass — the code-registered `example.trophy`'s own `useAction` ran: stamina `82.417 → 52.417` (delta **-30**) |
| A nested member that returns null is read as "not carried" by every consumer | pass — the cleaver's null `Gun`/`Battery`/`Wearable` left the game table at its defaults (tags `tool` with no `gun`, `destroyAtZeroCondition` false, `decayInfo` 0, `wearableCanBeHeld` false); the dressing's null `Tool` left its action to the authored path (`ItemActionInvoker`), not the tool closure |

The item table also showed all three definitions present with `usable = true` and a `useAction`, so the static
half of the mapping is visible in `Item.GlobalItems`.

## Substitutions and limits

- The example mod is a FIXTURE placed in the deployed plugins directory for the run and removed afterwards; the
  framework under test is the deployed artifact.
- The attack ran on the host's own body with the created item in a slot (the tool's delegate invoked directly
  through the game's table), not through a mouse swing; the value read is the mod's own `StaminaUse`, the exact
  claim the row makes.
- No OS-level mouse or keyboard input was used.
