# Acceptance — the mod item's wearable declaration: a live body wears the declared garment

- Ticket: `docs/backlog/done/mod-item-wearable-declaration.md` — verdict: **the native row passes, the ticket
  moves to `done/`**
- Batch: `.acceptance/` (local, gitignored; this interactive run's staging is `wex*`)
- Artifact: the deployed plugin at `0.1.0+5bc7241c…` plus the demonstration mod rebuilt at `eb05ec44`
  (the tree's only code change in this run is the example's declaration below)
- Date: 2026-10-24
- Result: **a live body wears the mod's declared garment; the built `ItemInfo` carries every field the game's
  wear flow reads.**

## Why this run needed a code change first (finding)

Staging the row found that the demonstration mod declared `Wearable => null` on **both** of its items, so the
row had no payload: the only mod items in the build were non-wearable, and forcing the native
`Body.WearWearable` on one of them threw `NullReferenceException` — the game's own behaviour for a non-wearable
item, not the row under test. `example.dressing` now declares the slice (limb `UpTorso`, slot `outertorso`,
`CanBeHeld`, armor 4, isolation 1.2, hit multiplier 0.75, visual offset 5), which is also what the API's
sample should show; the aggregate gate caught the file at 607 lines and the declaration plus two comments were
tightened until the class fits its 600-line ceiling (commit `eb05ec44`).

## The row

| Step | Evidence |
|---|---|
| the declared item is created and carries the mapped fields | `/give @s example.dressing` → `Gave example.dressing (#4615948227)`, and the probe reads the built `ItemInfo`: `wearable=true;desiredLimb=UpTorso;slot=outertorso` |
| the game's own wear path accepts it on a live body | the probe calls `PlayerCamera.main.body.WearWearable(item)` → `threw=false`, and the body's own query answers `GetWearable("example.dressing") != null` (`worn=true`) |
| the five statistics ride the same slice | `ItemWearableDeclarationTests.Item_DeclaringAPlacement_MapsEveryFieldTheGameReadsToPlaceAndWearIt` (in the run's behaviour suite, 6,395 passing) — the machine read above confirms the placement, the test pins the statistics |
| the incomplete declaration and the guard's refusal path | judged by their unit cases (`Item_DeclaringAWearableWithoutAPlacement…`, the negative-number refusal and the four guard readers' port-contract tests); they need no live body, and this run did not re-stage them |

## Limits and substitutions

- The wear call is the production `Body.WearWearable` invoked in-process rather than through the inventory
  gesture: the gesture is the game's own UI (a drag onto the body slot), and the row is about what the native
  wear flow does with a mod-declared garment, which is exactly the call the guard wraps. The gesture itself is
  unchanged and is not claimed here.
- The client ran without a lobby or a second player: the wear flow is local to the body, and the cross-player
  wear chain (`GameWearPlacement.TryResolve`) is covered by its own port-contract tests.
- The item was spawned with `/give` rather than found: spawning is the supported way to put a mod item into a
  body's hands, and the row reads the wear flow from there.
