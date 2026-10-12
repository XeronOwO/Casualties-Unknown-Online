# Acceptance record — Mod-authored effects: the code registration face and the operation surface it writes through

- Ticket: `mod-authored-effects` — verdict: **moved to `done/`**
- Batch: `20261011-k` — tickets: `mod-authored-effects`, `mod-item-authored-actions`,
  `mod-declared-behaviour-with-no-function` (plan: `20261011-k-scope.md`)
- Commit: `f9507771` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f950777142ed1e1177c48bee349a5e9b3040f4ec` (`verify-deploy.txt`: "Deployment matches this tree's
  build output")
- Run: 2026-10-11 22:25 → 22:35 · Host: physical machine, launched through Steam · Guest: primary sandbox ·
  two two-client sessions and one single-client session (the single-client run judged row K8, which the
  sibling record `mod-declared-behaviour-with-no-function-20261011-k.md` carries)
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present (`preflight.txt`: 11
  present, exit 0); `session-environment-status.txt` reads `active=cuo`, `game-running=false`
- Fixture: `CasualtiesUnknownOnline.ModExample.dll` from THIS tree's build output, placed in the install's
  `plugins` tree for the run and removed at its close (`fixture-placement.txt` records both hashes; the
  removal was re-read — the tree holds the owner's own mods again)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

The fixture is the in-repo example mod: `cuo.example` registers a drink effect for its liquid
`example.tonic` and declares a liquid tile `example.tonic.pool`, which the provider allocates a custom
world byte for. Two declared substitutions, both named where they are used below: the pool is PLACED by the
run (the definition's `SpawnAmount` is 0, so world generation never places it), and the guard rows'
throwing effect is supplied by a run probe through the product's own registration surface, because the
fixture ships no throwing function.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The example mod binds on both clients and all three of its registrations are ACCEPTED by the rails (`use=True, limb=True, drink=True`) | `machine` | **pass** | `f1-host-fixture-and-storm.txt`, `f2-guest-session1.txt`, `p2-guest-session2.txt`: `[Example] authored behaviour registered — dressing use=True, dressing limb=True, tonic drink=True.` on the host (22:25:22.715) and on the guest (22:26:05.205) |
| 2 | The live liquid registry entry carries BOTH native delegates (non-null) on both clients, although only the drink half is registered — the limb half is the framework's reporting delegate | `machine` | **pass** | `b1-host-defs.json`, `c1-guest-defs.json`: `"liquid":{"id":"example.tonic","present":true,"hasOnDrink":true,"hasOnHealthUse":true,"healthUsable":true,"injectable":true}` |
| 3 | The liquid tile is mapped to a custom world byte and the byte really enters the fluid grid: the drink of that cell runs CUO's own handler and the mod's REGISTERED drink effect on the drinking client's own body | `machine` | **pass** | `[LiquidTileContent] allocated world byte 7 for example.tonic.pool.` (f1); `d1-host-world-drink.json` reports `liquid 7, cell (512,986)`; `[LiquidTileDrink] drank custom liquid at=(512,986) type=7.` and `[Example] example.tonic drunk (200 ml): thirst 80.8 -> 60.8.` (f1, 22:27:12) — the effect landed on the client that drank, with the operation's own −20 |
| 4 | The load-time diagnostic names the half with no function: the provider says the liquid declares the flags and no limb effect is registered | `machine` | **pass** | f1 and f2, 22:25:22.740 / 22:26:05.228: `[LiquidContent] example.tonic declares HealthUsable/Injectable but no mod registered a limb-use effect for it — applying or injecting this liquid consumes it with no effect.` |
| 5 | The invoker's CALL guard: a throwing registered effect costs the effect and not the gesture | `machine` | **pass** | Forced setup `m1-host-plant-limb-guard.json` (`registered: true`, through `IModLiquidEffects.TryRegisterLimbUse`); the gesture `m5-host-wound-view-4b.json` + `m6-host-medical-guard.json` (`calls: "none"`, `dragAfter: "none"` — the release completed) then logged `[LiquidContent] cuo.example's limb use effect for example.tonic threw — the action completed without it.` (p1, 22:32:59.500) |
| 6 | The TICK guard: a throwing tick is skipped and the timed body keeps its schedule | `machine` | **pass** | p1: `[LiquidContent] cuo.example's timed effect k-guard2 threw on a tick — that tick was skipped and the effect keeps its schedule.` at 22:33:24.148 AND 22:33:25.154 — two skipped ticks one second apart, so the loop survived the first throw; the same line was read once more for the liquid half's own timed body (`k-guard`, 22:33:01.487) |
| 7 | The paths this change touches logged no unintended error; every error-level line of the run is attributed | `machine` | **pass** | p1/p2: the run's error lines are (a) the two planted guard throws, which are rows 5/6's own evidence by design, and (b) `ItemInfo.HasTag` exceptions from the fixture item that declares no tags — finding F1, filed as `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` with this run's measurements. Nothing came from the effect registration, the invoker's happy path or the tile handler |

## Residuals for the user

None. Every row is a machine read this run produced.

## Limits

- **The liquid's limb-effect landing is not this fixture's row.** The example mod registers only the
  tonic's drink half, deliberately (the limb half is the sibling ticket's "declared with no function"
  shape), so what the run judges is the drink half landing on the right body and the limb half reaching the
  reporting delegate. A fixture that registers a limb effect does not exist in this tree.
- **The injection call site was not driven.** `WaterContainerItem.Inject` needs the syringe minigame, which
  no committed recipe stages. Row 2's live non-null `onHealthUse` delegate is the reading that covers that
  call site's precondition; the gesture itself is not claimed.
- **The guards were judged with a run-planted throwing effect.** The fixture ships none, so the probe
  registers one through the product's own `IModLiquidEffects` rails (the registration verdict is part of the
  evidence) and the result was promoted into `tools/acceptance/recipes/plant-throwing-effect.cs` so the next
  run does not rebuild it. What that proves is the invoker's and the tick loop's own behaviour, not that a
  shipped mod throws.
- **Two clients, one world; the third client was not launched** (no third-peer row). The rendered and
  first-person halves of the liquid are not claimed by any row.
- Row 3's cell is the cell the body stood in, so its coordinates are the run's, not the pool's placement:
  nothing places the pool in world generation by design.

## Closing state

Both clients quit through the game (0 game processes left), the fixture DLL was removed from the plugins
tree and the removal re-read, and the run's artifacts stayed in the batch directory. The reusable half of
the run moved into `tools/acceptance/recipes/plant-throwing-effect.cs`; the machine facts and the lessons
the run taught went into `docs/acceptance/AGENTS.local.md` and `docs/acceptance/lessons.md`.
