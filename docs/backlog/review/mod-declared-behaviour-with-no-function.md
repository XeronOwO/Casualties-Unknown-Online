# A declared behaviour with no function: a mod item's use and a mod liquid's effect

- Status: Review — **cut 2026-10-08** out of `mod-content-ceiling.md` Part 3.A, which lists the liquid effect
  delegates as a content-surface field gap and leaves one open question: whether the native path refuses a
  missing effect or throws. **It throws** (see *Current readings*), so that half is a defect of a surface the
  API already promises rather than a new capability. The family audit run while cutting it found the item
  twin (`Usable` / `UsableWithLmb` with no `useAction`), and Part 3.A's own rule — fix the family, not the
  reported case — puts both halves in this one cycle. Landed 2026-10-08 (see *What landed*): the code half is
  complete, the shape was ruled by the user and the independent review raised no blocker or major, so what
  remains is the acceptance batch's own row and nothing to develop.
- Priority: High
- Category: Mod platform / content binding
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 3.A)
- Related: `docs/en/reference/mod-api.md` (the two DTO rows this ticket's surface promises),
  `docs/backlog/review/mod-content-kind-with-no-provider.md` (the precedent: a declaration nothing
  materializes), `docs/backlog/review/mod-crafting-quality-labels.md` (the other one: a reference nothing
  provides, refused at load), `docs/backlog/review/mod-cross-player-native-semantics.md` (the chains whose
  appliers already guard a delegate-less liquid), `docs/decisions/active.md` (184: the affected side judges
  on its own picture)
- Source: the 2026-09-25 mod-ceiling inventory, promoted 2026-10-07; this stage cut on 2026-10-08 because the
  ceiling ticket's own plan says each stage is its own deliverable. The item twin is this cycle's own family
  audit, with the same native evidence shape.

## Why this ticket exists

A mod can declare, through the content API, that its item is usable and that its liquid can be applied to a
limb or injected. The API cannot carry the function that would run — `Abstractions` has no game type, by
construction — and the game's own call sites do not null-check. So the declaration reaches the game, the
game calls a delegate that is not there, and the player's action dies with a `NullReferenceException`.

That is the "declaration versus materialization" defect this project has closed twice already
(`mod-content-kind-with-no-provider`, `mod-crafting-quality-labels`), in its sharpest form: those two were
silent, this one is a crash on the game's own path.

## User-visible behaviour and current readings (measured 2026-10-08)

**What a player sees today.** With a mod that declares `ModItemDefinition.Usable` and no `Tool`/`Gun`
behaviour, using that item from the inventory throws before the game's use action completes. With a mod that
declares `ModLiquidDefinition.HealthUsable` (or `Injectable`), applying that liquid to a limb — or injecting
it — throws the same way. With ANY mod liquid in a container, drinking it throws, because the drink call site
is not gated by any flag at all. And a mod's own liquid tile can be drunk too
(`ModLiquidTileDefinition.LiquidId`, whose doc says "Logical liquid content id (vanilla or a registered
`ModLiquidDefinition`)"): CUO's own handler for custom world bytes calls the same missing delegate.

**Where the missing function is.**

- The item mapping — `GameAdapterItemContentProvider.BuildItemInfo` as measured, now
  `ModItemInfoFactory.Build` (see *What landed*) — set `usable = definition.Usable` and
  `usableWithLMB = definition.UsableWithLmb`, and assigned `info.useAction` only inside its `definition.Tool`
  and `definition.Gun` branches. `ItemInfo.useAction` is a plain public field of delegate type
  (`reversing/Assembly-CSharp/Assembly-CSharp/ItemInfo.cs:109`), so it stays null.
- `GameAdapterLiquidContentProvider.BuildLiquid` maps the static fields (`healthUsable`, `injectable`,
  `injectionSickness`, colour, value, `qualities`) and assigns neither delegate. The type's own doc comment
  states the rule: "behavior callbacks are intentionally not part of this DTO because mods must not pass game
  delegates through Abstractions". Nothing else in CUO assigns them either.

**Where the game calls it, unguarded** (every line below is a direct call with no null check; the census is
the `onDrink` / `onHealthUse` / `useAction` call sites in `reversing/`, and this is all of them):

| Call site | Guard before the call | Mod-content reachable |
|---|---|---|
| `Body.cs:2479` `item.Stats.useAction(this, item)` | `if (item.Stats.usable)` | yes — `ModItemDefinition.Usable` |
| `Body.cs:2454` `Stats.useAction(this, GetItem(num))` | `... Stats.usable && Stats.usableWithLMB` | yes — `Usable` + `UsableWithLmb` |
| `WaterContainerItem.cs:210` `liquidType.onDrink(list[i], body)` | the liquid is in the registry | yes — ANY mod liquid |
| `WaterContainerItem.cs:230` `liquidType.onHealthUse(list[i], limb)` | `&& liquidType.healthUsable` | yes — `ModLiquidDefinition.HealthUsable` |
| `WaterContainerItem.cs:256` `liquidType.onHealthUse(list[i], limb)` | `&& liquidType.injectable` | yes — `ModLiquidDefinition.Injectable` |
| `FluidManager.cs:293-308` (4 calls) | hardcoded vanilla ids (`groundwater`, `lumalgae`, `oil`, `sap`) | no |
| `NonDescriptCan.cs:78` | the can's own `liquidIds` array | no |
| CUO's `WorldGen/LiquidTileDrink.cs` `liquidType.onDrink(200f, body)` | the world byte maps to a registered liquid | yes — a mod liquid tile over a mod liquid |

**Where CUO already guards.** The three cross-player appliers refuse to call a delegate that is not there,
and say so in the same shape: `NativeDrinkApply` — "native `Drink` calls `onDrink` without a null check, so
a liquid that carries no delegate would throw", logs `dose skipped: liquid {Liquid} carries no onDrink
delegate — native Drink would throw here.`; `NativeInjectionApply` — "an injectable liquid that carries no
delegate throws", same log shape; `NativeTopicalApply` — the same for `onHealthUse`. So the family's own
reviewed answer on CUO's side is *log and skip*; only the game's own path and CUO's tile handler lack it.

**What the two flags actually buy today** (this is what the fix must not misrepresent):

- `LiquidType.injectable` gates ONLY the effect delegate: `WaterContainerItem.Inject` applies
  `injectionSickness` sickness and the blood-viscosity term regardless of it, so a mod liquid's data half of
  an injection already works. `healthUsable` gates the delegate AND `ItemInfo.ActuallyUsableOnLimb`
  (`ItemInfo.cs:10-25`), i.e. whether a container holding it can be offered on a limb at all.
- `ItemInfo.usable` gates the call and is also what CUO's own eligibility reads
  (`LocalUseItemEligibility.IsUseItem` → `FamilyOf` → `ConsumeAdmission` / `GameSolidFoodFacts`), so the item
  is admitted by CUO's chains only because its own data says so.

## Entry mapping

Every entry that can run a mod-declared behaviour, with the native call site it is the counterpart of. The
mapping is what decides the fix: the function is information NO gesture can carry, so the answer may only be
"refuse it" or "ask the user" — never a fallback that supplies one.

| Entry (CUO gesture, or the game's own when CUO has none) | Native call site it mirrors | What the entry needs that its gesture cannot carry | Verdict |
|---|---|---|---|
| The inventory use of the item in hand (radial drag / LMB) — CUO's counterpart gesture is the world drag onto a teammate | `Body.UseItem` (from `PlayerCamera.cs:1646`) and `Body.UseItemInHand` (`Body.cs:2449`) → `ItemInfo.useAction` | a use action; a mod item can declare `Usable` but no function exists to run | the game's own path runs the mod's declaration — must be defined, not thrown |
| The wound view's release onto a limb — CUO's counterpart is the medical view's limb release (decision 246) | `PlayerCamera.ApplyWoundItem` → `WaterContainerItem.ApplyToLimb` (`:218-234`) → `onHealthUse` | an effect function for the liquid being applied | same |
| Drinking a container — CUO's counterpart is the cross-player drink chain (world drag / held-remote-item route) | `WaterContainerItem.Drink` (`:198-215`) → `onDrink` | an effect function; the drink call site is gated by NO flag, so the entry exists for every mod liquid | same |
| Injecting a container — CUO's counterpart is the medical operation session | `WaterContainerItem.Inject` (`:237-261`) → `onHealthUse` | an effect function | same |
| Drinking from a world fluid cell — CUO's counterpart is `LiquidTileDrink` for custom world bytes | `FluidManager.cs:293-314` → `onDrink` | an effect function; CUO's own handler calls it for a mod's own liquid tile | CUO's own call site — ours to guard |
| CUO's cross-player application of any of the above | the same native bodies — `Body.UseItem` / `Body.UseItemInHand`, `WaterContainerItem.Drink` / `ApplyToLimb` / `Inject` — run on the affected player's own client | the same function | already guarded (`NativeDrinkApply` / `NativeTopicalApply` / `NativeInjectionApply`) |

The entries stay isolated in THIS change: it adds no entry and no new arbitration, and it makes the entries
that already exist well-defined for content that declares them. That is a claim about the change, not about
the file: `LocalUseItemEligibility` is still an if-else ladder, and a mod liquid declaring BOTH
`HealthUsable` and `Injectable` is claimed by the injection rule by position there — the pre-existing order
the consume chain records ("the order the routing sites ask them in, injection first",
`review/mod-cross-player-native-semantics.md`). Nothing here moves it, and this ticket does not claim it is
gone.

## Mechanism inventory

| # | Mechanism | Evidence | State |
|---|---|---|---|
| 1 | Mod item flags → `ItemInfo.usable` / `usableWithLMB` with a null `useAction` | `ModItemInfoFactory.Build` (then `GameAdapterItemContentProvider.BuildItemInfo`): "usable = definition.Usable"; `useAction` assigned only in the `Tool` and `Gun` branches; `ItemInfo.cs:109` field default null | defect |
| 2 | Mod liquid flags → `LiquidType.healthUsable` / `injectable` with null delegates | `GameAdapterLiquidContentProvider.BuildLiquid` maps static fields only; the provider doc comment states callbacks are deliberately absent | defect |
| 3 | The game's unguarded call sites | the five rows of the table above (plus two unreachable ones, named) | defect (consequence) |
| 4 | CUO's cross-player appliers | `NativeDrinkApply` / `NativeTopicalApply` / `NativeInjectionApply` log and skip | already correct |
| 5 | CUO's own custom-byte drink handler | `LiquidTileDrink.TryDrink` calls `onDrink` unguarded | defect (ours) |
| 6 | Reachability | item: radial drag / LMB on a declared-`Usable` mod item; liquid: a container holding a mod liquid (the mod's own liquid tile fills it — the provider maps the custom world byte through `FluidManager.WorldFluidToLiquidID` to `FillLiquidId`), and the mod's own tile drunk directly | reachable, no third-party mod needed beyond the content itself |

## Plan

1. **Decide the shape** (the user's ruling, below): what a declared behaviour with no function does.
2. **Make both build sites materialize a defined behaviour** — `ModItemInfoFactory.Build` and
   `BuildLiquid` — so that no
   object CUO registers can reach an unguarded native call with a null delegate. The delegates CUO installs
   are inert and self-reporting (they name the content id and say why nothing happened), so the branch is
   observable rather than silent.
3. **Guard CUO's own `LiquidTileDrink`** with the same null rule the three appliers already use, so a
   registry entry CUO did not build (a tier-3 registration) cannot crash our handler either.
4. **Diagnose the declaration at load time**, in the shape the two precedents established: a warning naming
   the definition and the flag whose function cannot be authored today.
5. **Red first**: behaviour tests through the existing reflection door onto the two build sites — they fail
   on the frozen tree because the built object carries no delegate. Invoke the installed delegate with the
   game objects a test can supply and assert it does not throw.
6. **Whole family**: keep the three appliers' guards and pin what the framework CONSTRUCTS, so a future
   content flag cannot re-open this shape silently. Delivered over `src/`, not as the `reversing/` census
   this plan first named: that tree is gitignored, so a gate reading it would fail on every checkout that
   lacks it. The game's own call sites live in the gate's doc comment instead, and the rule it enforces is
   the construction site (see *What landed*).
7. **Docs**: the two `mod-api.md` DTO rows and the Chinese block page (`docs/en/` + `docs/zh/` in the same
   change), the ceiling ticket's Part 3.A and its answered open question, and this ticket's status.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Item build site (#1) | a reporting `useAction` for a declared-usable item | the red was observed against the PRE-SPLIT site (`GameAdapterItemContentProvider.BuildItemInfo`), which the delivered case no longer names, so it is recorded here and not reproducible from the frozen test; the case is green at `ModItemInfoFactory.Build` |
| Liquid build site (#2) | both delegates on every built liquid | `Liquid_BuiltWithNoAuthoredEffect_…` red → green; `Liquid_DrinkingWithNoAuthoredEffect_…` and `Liquid_AppliedToALimbWithNoAuthoredEffect_…` invoke the installed delegate and assert the line it reports |
| CUO's tile handler (#5) | a missing delegate is refused instead of called | source-verified only: `TryDrink` needs a live `FluidManager`, which the test host cannot build, so L0 cannot reach it — it rides the acceptance row |
| The appliers (#4) | unchanged | their skip branch is now unreachable for framework-built content (every liquid carries a delegate); reaching it needs a live `Body`, so no L0 case drives it, and the full suite is the evidence that nothing else moved |
| Mutation | the item-side install deleted | the gate failed (1 of 9) and the item case failed (1 of 6); both green again after the restore |
| Reachability (#6) | the end-to-end run | acceptance row: a mod-declared item used, and a mod liquid drunk / applied, in a real session — needs a throwaway mod fixture (*Limits*) |

## The ruling (2026-10-08)

The engine cannot carry a mod-authored function, so a declared behaviour had to resolve one of two ways, and
the user ruled the **materialize-and-report** shape: the action proceeds and consumes what it would have
consumed, nothing changes, and one log line names the content and says its effect function does not exist.
Their own direction in the same session decided it — they promoted `mod-authored-effects.md` (a code
registration face plus an engine-typed handle) as the capability that makes those flags meaningful, and the
refuse-the-declaration shape would have taken away exactly the surface it builds on. The drink path settled
it independently: no flag gates `WaterContainerItem.Drink`, so there was nothing to refuse there under any
ruling.

Not in scope, by the parent ticket's own gate: giving mods the function itself — that is
`mod-authored-effects.md`, now promoted on its own.

## What landed

- `ModItemInfoFactory` (new) owns the mod definition → `ItemInfo` mapping — extracted from the item provider,
  which was at 579 lines against the 600-line structure gate — and installs a reporting use action for a
  definition that declares usability without a `Tool` or `Gun` behaviour.
- `GameAdapterLiquidContentProvider.BuildLiquid` installs both delegates on every liquid it builds;
  `TryBind` reports a declared `HealthUsable` / `Injectable` at load, and the item provider reports its own
  declaration the same way.
- `LiquidTileDrink` refuses a custom world byte whose liquid carries no `onDrink`, with a log — the rule the
  three cross-player appliers already applied to their own paths.
- `DeclaredBehaviourContentTests` (6 cases) drives both build sites reflectively; the red was observed first:
  4 failures, each `Assert.NotNull: Value is null` on the built object's delegate, with the two guard cases
  green.
- `ContentBehaviourDelegateGateTests` (1 fact + 10 matcher cases) requires every `ItemInfo` / `LiquidType`
  constructed under `src/` to be built by a member that installs the delegate, and counts an assignment a
  definition slice's own branch gates as that slice's, not as the default.
- `docs/en/reference/mod-api.md` and `docs/zh/reference/mod-api.md` carry the rule, and the pair's alignment
  record is re-confirmed at the new contents.

## Limits

- The end-to-end half needs a mod that declares these flags; the sample mod
  (`src/CasualtiesUnknownOnline.ModExample/`) declares no item or liquid content, so the acceptance row
  needs a throwaway mod fixture built for the batch. Named rather than replaced by a weaker check.
- A tier-3 plugin that writes into `Liquids.Registry` / `Item.GlobalItems` itself is outside the content
  API's promises; only CUO's own handler (#5) is guarded against it.
- The tile handler's refusal and the three appliers' skip branches need live Unity objects (`FluidManager`,
  `Body`), so no L0 case drives them; the handler's path is an acceptance row.
- The reporting delegates log at `Warning` on every use — the level the appliers' own skip line already
  used — so a player who drinks a mod liquid repeatedly logs repeatedly. A load-time notice alone would leave
  the event that matters, the effect not happening, unobserved at the moment it happens.
- The item half's red was observed BEFORE the `ModItemInfoFactory` extraction, against the build site the
  extraction then moved; the delivered case names the new home, so it cannot reproduce that red. The
  extraction is behaviour-preserving apart from the install itself, which is what makes the recorded red the
  same red.
- A mod liquid that declares an effect flag now has a delegate that reports and does nothing, so CUO's own
  cross-player appliers call it rather than taking their "carries no delegate" branch. The user-visible
  outcome is the same (the ml is spent, nothing changes); the log line that says so is now the delegate's.

## Non-goals

- Not the mod-authored effect-function surface (the parent ticket's Stage 3).
- Not the other Part 3.A field gaps (`useLimbAction`, the wearable set, the miscellaneous reads) — each keeps
  its own consumer rule. **The wearable half of that sentence was WRONG and is corrected 2026-10-11**: it is
  a crash, not a silent gap. `Body.WearWearable` resolves the limb name `ItemInfo.desiredWearLimb` carries
  with `Body.LimbByName` and dereferences the result on the next line (`Body.cs:1493-1494`), and nothing
  filled that field for a mod item — so a definition declaring `Wearable` threw inside the game's own wear
  flow the moment a player put it on. It is `docs/backlog/review/mod-item-wearable-declaration.md` now, with
  the same native-evidence shape this ticket used, and its own finding was made while auditing this family.
  The `useLimbAction` half stays a non-goal for a different reason: its consumer IS a delegate, and
  `Abstractions` cannot carry one — that is `docs/backlog/todo/mod-authored-effects.md`.
