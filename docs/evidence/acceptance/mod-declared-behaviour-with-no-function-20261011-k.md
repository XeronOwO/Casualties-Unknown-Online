# Acceptance record — A declared behaviour with no function: a mod item's use and a mod liquid's effect

- Ticket: `mod-declared-behaviour-with-no-function` — verdict: **moved to `done/`**
- Batch: `20261011-k` — tickets: `mod-authored-effects`, `mod-item-authored-actions`,
  `mod-declared-behaviour-with-no-function` (plan: `20261011-k-scope.md`)
- Commit: `f9507771` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f950777142ed1e1177c48bee349a5e9b3040f4ec`
- Run: 2026-10-11 22:25 → 22:35 · Host: physical machine · Guest: primary sandbox · a third single-client
  session served the tile-refusal row
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present (`preflight.txt`);
  `session-environment-status.txt` reads `game-running=false`
- Fixture: the in-repo example mod's DLL from this tree's build output, placed in the install's `plugins`
  tree for the run and removed at its close (`fixture-placement.txt`)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

The ticket's reach row needs a mod that declares a behaviour and supplies no function, and the fixture now
ships both halves: `example.placebo` (item, `Usable`, nothing registered) and the tonic's LIMB half (liquid,
`HealthUsable` + `Injectable` declared, only the drink half registered). The container route is the game's
own: a carried bottle standing in the custom world byte auto-fills through
`WaterContainerItem.Update` → `FluidManager.WorldFluidToLiquidID`, and the wound view then takes its
container branch into `ApplyToLimb` → `onHealthUse`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Both build sites install a delegate on every object CUO builds: the declared-usable item carries a reporting `useAction`, the mod liquid carries BOTH native delegates, on both clients | `machine` | **pass** | `b1-host-defs.json`, `c1-guest-defs.json`: `example.placebo` `"hasUseAction":true`; `example.tonic` `"hasOnDrink":true,"hasOnHealthUse":true` — the second one although only the drink half is registered |
| 2 | The load-time report names the declaration: the item provider says the item declares usability and carries no function | `machine` | **pass** | `f2-guest-session1.txt`, 22:26:32.784: `[ItemContent] example.placebo declares Usable/UsableWithLmb and carries no Tool or Gun behaviour, and no mod registered a use action for it — using the item will do nothing. Register one through IModItemActions in Bind.`; the liquid half's own line is in the sibling record (row 4) |
| 3 | Using a declared-usable item with NO registered action runs the framework's reporting delegate and completes: the warning names the content and the half, the item is still held, the gesture does not throw | `machine` | **pass** | `e2-host-use-placebo.json` (`usable: true`, `condition: 1`, `heldAfter: true`) and `[ItemContent] example.placebo was used (use) with no effect — no mod registered one for that half.` (f1, 22:27:21.062) |
| 4 | Applying a container that holds the mod liquid to a limb reaches the liquid half's reporting delegate, and the container is spent as a real application would spend it | `machine` | **pass** | The bottle's own read after the auto-fill: `i6-host-bottle-refilled.json` — `stacks: [{"example.tonic", 200}]` filled from the world byte (`i5-host-fill-again.json`: `byteBefore 0, byteAfter 7`); gesture `j1-host-wound-view-3.json` + `j2-host-medical-tonic.json` (`calls: "none"`, `dragAfter: "none"`) then `[LiquidContent] example.tonic was used (limb use, 100.0 ml) with no effect — no mod registered one for that half.` (p1, 22:32:18.004) — the application's own reported dose is the 100.0 ml a real effect would have received, which is what the delegate's line carries; the bottle's own post-apply read is not in this run's evidence |
| 5 | CUO's own custom-byte drink handler REFUSES a registry entry whose liquid carries no `onDrink`, instead of throwing, and leaves the cell alone | `machine` | **pass** | Forced setup `q3-host-tile-refusal.json`: the probe cleared the live delegate (`hadDelegate: true`), wrote byte 7 into the body's cell, drank it through `FluidManager.DrinkLiquid` and restored the delegate — `byteBefore 7, byteAfter 7, restored: true`; the handler's own line, landed as `q4-host-tile-refusal-log.txt`: `[LiquidTileDrink] custom liquid at=(512,512) type=7 carries no onDrink delegate — the drink is refused.` (22:34:38.958) |
| 6 | The paths this change touches logged no unintended error; every error-level line is attributed | `machine` | **pass** | p1/p2: the reporting paths and the tile handler logged warnings by design and nothing at error level; the run's error lines are the planted guard throws (sibling records) and the `ItemInfo.HasTag` finding, filed as `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` |

## Residuals for the user

None. Every row is a machine read this run produced.

## Limits

- **The drink half of the reporting path has no fixture liquid.** `WaterContainerItem.Drink` calls
  `onDrink` for every stack with no flag, and every liquid CUO builds carries a delegate — but the tree's
  only mod liquid registers its drink half, so what the run judges on that call site is the REGISTERED
  effect (sibling record, row 3). The unregistered drink half is covered by the L0 case
  (`Liquid_DrinkingWithNoAuthoredEffect_…`) and by reading the live built object (row 1); it has no
  acceptance row in this run.
- **The injection call site was not driven** (`WaterContainerItem.Inject` needs the syringe minigame, which
  no committed recipe stages). The tonic declares `Injectable` so the call site is not gated off, and row 1
  reads the delegate it would run; the gesture itself is not claimed.
- **Row 5 is a forced state, not a tier-3 plugin**: the probe cleared the delegate on the live registry
  entry for one call and restored it inside the same eval, which reproduces the state the handler guards
  (a registry entry CUO did not build). A real third-party registration is not shipped by this tree.
- **Two clients, one world** in the two-client sessions; the tile-refusal row ran single-client, and no row
  here depends on a peer view.
- The fixture item's own `Tags` defect (the finding above) is not this ticket's change; row 3's reporting
  line was read before it fires and the row is about the reporting delegate's own behaviour, which the
  finding does not touch.

## Closing state

Both clients quit through the game (0 game processes left), the fixture DLL was removed and the removal
re-read, and the run's artifacts stayed in the batch directory. The reusable shape this ticket's fixture
needed — rooting out the example mod's own context to plant a function through the product's registration
surface — moved into `tools/acceptance/recipes/plant-throwing-effect.cs`.
