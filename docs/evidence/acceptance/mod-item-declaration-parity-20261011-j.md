# Acceptance record — The item declaration reaches every field the game reads

- Ticket: `mod-item-declaration-parity` — verdict: **moved to `done/`**
- Batch: `20261011-j` — tickets: `mod-item-declaration-parity` (plan: `20261011-j-scope.md`)
- Commit: `03b8e493` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+03b8e4930cd53eac14bcf6d3a0dc6d5127b8e566`
- Run: 2026-10-11 21:43:42 → 21:45:50 · Host: physical machine, launched through Steam · Guest: primary
  sandbox
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present (11 present, exit 0)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

The fixture is the in-repo example mod's attribute-declared `example.dressing`: its recognition gate is the
declaration this ticket made expressible, so the rows read it out of each client's own `Item.GlobalItems`
and then ask the game's own hover what it makes of a live instance. Two declared substitutions:

- **the reading body's INT is forced to 0** (`body-force intelligence=0`, a forced body write), which is the
  state the defect is about: the data class's leftover gate is 2, so INT 0 is BELOW it, and the pre-change
  code would answer "unrecognized" here. The forced value is read back in the same step (`intelligence: 0`).
- **the item instance is created through the game's own grant path** (`item-provide mode=create`, which runs
  `Utils.Create` + `Body.PickUpItem`) rather than placed by the tooling; the created ids are host
  `1142782281667` and guest `9492956821`.

The fixture DLL (`CasualtiesUnknownOnline.ModExample.dll`, built from `03b8e493`) was placed in the install's
`plugins` tree for the run — the same place the in-repo pinyin mod occupies — and removed at the close; it is
not part of the product's deployment.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | `Item.GlobalItems["example.dressing"]` exists on BOTH clients and its recognition gate is the DECLARED one (0), not the inherited 2 | `machine` | **pass** | `b1-host-definition.json`, `b3-host-read.json`, `b5-host-read-int0.json`, `c3-guest-read-int0.json` — every one reports `"present": true, "recognitionMin": 0`; both logs carry `[ItemContent] injected example.dressing into Item.GlobalItems (1 crafting qualities).` (`d1-log-census.txt`) |
| 2 | The game's own recognition answer is true on each reading body (`rec.min <= skills.INT`), so the hover takes the item's-name branch | `machine` | **pass** | `b4-host-int0.json` / `c1-guest-int0.json` report `intelligence: 0`; `b5-host-read-int0.json` and `c3-guest-read-int0.json` then report `"bodyInt": 0, "recognizable": true` |
| 3 | The hover text of a live instance names the item and carries its description instead of `unknownobject` / `unknownobjectdsc` | `machine` | **pass** | `b5-host-read-int0.json`, `c3-guest-read-int0.json`: `hoverName` `Example Dressing (100%)`, `hoverDescription` the author's own sentence plus the game's weight line — no unknown-object text and no INT warning. Positive control, same body state and same probe: `b6-host-control-bandage.json` reads a VANILLA item whose gate is 1 (`"recognitionMin": 1, "recognizable": false`) and gets `某种医疗物品…` with `你智力太低了，你无法理解这是什么（需要 1 智力）` — the branch and the printed requirement flip with the gate |
| 4 | Neither client's item-provider logging carries a refusal, and the acceptance line names the definition | `machine` | **pass** | `d2-log-census-final.txt`: `[ItemContent] … refused` count 0 on both; the accepted/injected lines for `cuo.example/example.dressing` are present on both |
| 5 | Neither client logs an error from the item provider or from this change's paths over the run | `machine` | **pass** | `d2-log-census-final.txt`: one error-level line per client, attributed below and outside every path this change touches; the item provider's own lines are all `[Info]` |

## The one error-level line in each log, attributed

Each client logged exactly one error-level line, at startup (21:43:45 host, 21:43:57 guest — before the
lobby existed and before any probe): a `TypeLoadException` for
`System.ComponentModel.DataAnnotations.RequiredAttribute` while resolving a JSON contract. Its stack names the
origin end to end and it is not CUO's item path:

```text
Newtonsoft.Json.Serialization.DefaultContractResolver.CreateContract
NJsonSchema.Generation.JsonSchemaGenerator.Generate
NJsonSchema.JsonSchemaBuilder... → HotRepl.Control.Schema.SchemaCache.BuildCompiledSchema
HotRepl.Control.Schema.SchemaCache.For
```

It is the acceptance tooling's own schema generator (HotRepl resolving a .NET Standard facade its Mono
cannot load), it is identical on both clients, and the deploy carries that assembly for CUO's own
configuration binder, which this run did not exercise. It is recorded rather than dismissed; see *Limits*
for what could not be proven about it.

## Residuals for the user

None. Every row is a machine read the run produced, and no row asks for a person's judgment.

## Limits

- **The tooltip's positive-gate half is not exercised by this fixture.** The warning line that prints the
  required INT is only reachable for a declaration with a POSITIVE gate, and this fixture declares 0 — which
  is the fix's own default and the half the ticket is about. The control above reaches that line through a
  vanilla item, so the run shows both branches; what it does not show is a MOD item declaring a positive gate
  and printing it. That declaration is one member away and was not in the fixture.
- **The startup `TypeLoadException` could not be A/B'd.** Proving it pre-existing needs a run without HotRepl,
  which the acceptance channel is built on. What the run establishes is narrower and stated as such: the line
  is not from the item provider, not from any path this change touches, and it fires before the world loads.
- **One world, two clients, one session, ~2 minutes.** No third-peer view was taken (no row claims one), and
  the rows here are states rather than timings, so repetition adds nothing.
- **`body-force intelligence=0` is a forced write**, not the game's own mindwipe: the real path
  (`MindwipeScript`) also clears every recipe's made-before flag and knocks the body down, none of which any
  row here needs. What matters is the one field the gate reads, and it is read back.

## Closing state

Both clients quit through the game (0 game processes left), the fixture DLL was removed from the plugins tree
and the removal re-read, and the run's own artifacts stayed in the batch directory. The reusable half of the
run — a probe that reads one item definition out of the game's table and asks the game's own hover about a
live instance of it — moved into `tools/acceptance/recipes/mod-item-read.cs`, and the body-force recipe
gained the `intelligence` field the gate reads.
