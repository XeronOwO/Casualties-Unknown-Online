# Cross-player semantics from the game's own data

- Status: Todo — **cut 2026-10-08** out of `mod-content-ceiling.md` Part 2 stage 2, which required this
  stage to become its own architecture ticket. Part A (the injection chain) landed in the same cycle;
  Part B carries the remaining chains.
- Priority: High
- Category: Mod platform / remote medical / architecture
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 2, stage 2)
- Related: `docs/backlog/review/remote-medical-stage-1-injection-session.md` (the session this ticket's
  Part A changes the effect half of), `docs/backlog/review/mod-crafting-quality-labels.md`,
  `docs/backlog/review/mod-content-kind-with-no-provider.md`, `docs/en/reference/mod-api.md`,
  `docs/en/reference/modification-policy.md`, `docs/decisions/active.md` (184: the affected side judges
  on its own picture)
- Source: the 2026-09-25 mod-ceiling inventory (Part 2), promoted 2026-10-07; this stage was cut on
  2026-10-08 because the ceiling ticket's own plan says each stage is its own deliverable.

## Why this ticket exists

Every "use this on another player" chain is a hand-written allowlist of vanilla ids plus the effect
formulas copied out of the game. The injection chain was the worst case: `RemoteMedicineCatalog` carried
15 container ids with a per-item ml amount, 25 liquids with per-ml coefficients and timed durations, and
a `water` inert list, transcribed from `WaterContainerItem.Inject`, `Item.cs`'s per-item `useLimbAction`
lambdas and the `Liquids.cs` `onHealthUse` bodies.

Two consequences, both already recorded in the ceiling ticket:

- **The ceiling is our maintenance speed, not the game's capability.** Content the tables do not carry —
  including every mod item and liquid — could be carried, dropped, traded and saved, but had no
  cross-player semantics. The game has 26 injectable liquids; the table carried 20 and the item table 15.
- **The transcribed constants are patch-stack debt.** A game update that moves a formula moves the game
  and leaves the copy behind, silently.

## What the game itself offers (measured 2026-10-08, `reversing/Assembly-CSharp/Assembly-CSharp/`)

| Native fact | Where | What it gives us |
|---|---|---|
| `PlayerCamera.ApplyWoundItem(Item)` | `PlayerCamera.cs:739` | the whole native limb-action dispatch: `usableOnLimb` → `item.Stats.useLimbAction(limb, item)`, else a `WaterContainerItem` → `ApplyToLimb(limb, 100f)` |
| `WaterContainerItem.Inject(Limb, float)` | `WaterContainerItem.cs:237` | the only injection implementation in the game: per stack `injectionSickness` sickness and blood viscosity, `injectable` gate, then the liquid's own `onHealthUse(ml, limb)`; the drain is `CalculateDrain` (proportional, capped at the container total) |
| `LiquidType` fields | `LiquidType.cs` | `injectable`, `healthUsable`, `injectionSickness` are data; `onHealthUse` / `onDrink` are delegates |
| `Liquids.Registry` | `Liquids.cs:1846` | id → `LiquidType`, the game's own content registry |
| `Item.GlobalItems` + `ItemInfo.ActuallyUsableOnLimb` | `Item.cs:272`, `ItemInfo.cs:10` | id → `ItemInfo` (with `usableOnLimb`, `LiquidItemInfo.capacity`); a liquid container is usable on a limb when it holds a `healthUsable` liquid or its own `usableOnLimb` is set |
| `SyringeMinigame.OnUse(speedMult)` | `SyringeMinigame.cs:92` | called every minigame frame with `depth * Time.deltaTime`; the item's own `useLimbAction` multiplies that by its own ml/s literal |
| `CoUtils.DoTimedOp(id, op, duration)` | `CoUtils.cs:53` | the same id ACCUMULATES `remaining`, so per-frame native doses sum exactly like one terminal dose |

Two facts that decide the design:

- **The injection amount is not data.** It is an `ldc.r4` literal inside each item's `useLimbAction`
  closure (`mult * 100f` / `150f` / `80f`, or a fixed `50f` / `33.334f` / `375f` / `100f`), and it is a
  rate (ml per second of full plunger depth), not a per-use dose. No field carries it; the only runtime
  source is the native call itself.
- **`usableOnLimb` alone does not separate injection from topical** — `paincream`, `woundglue`,
  `disinfectant` and `spraybottle` are `usableOnLimb` too, and their delegates call `ApplyToLimb`. What
  separates them is the LIQUID: `injectable` liquids are injected, `healthUsable` liquids are applied.

## Part A — the injection chain (landed 2026-10-08)

### The predicate

The host and the operator's own client both ask the game's data instead of a table:

- item: `Item.GlobalItems[itemId].Stats` is a `LiquidItemInfo` whose `usableOnLimb` is set;
- liquid: at least one stack's `Liquids.Registry[liquidId].injectable` is set.

The runtime cannot reference game assemblies, so the host's half goes through a new
`ILimbUseSemantics` seam (Runtime) implemented by the Game Adapter over `Item.GlobalItems` /
`Liquids.Registry`, and the operator's half (also Game Adapter) answers through the SAME registered
seam instance, so one predicate exists, not two. The mixed-container liquid allowlist and the inert
`water` list are gone: inside a container this chain admits, a non-injectable liquid is drained and
applies nothing, which is the game's own behaviour rather than an approximation. The routing predicate
still needs ONE `injectable` stack, because that is what keeps the topical family (also `usableOnLimb`,
its delegate calling `ApplyToLimb` instead) out of this chain — an entirely inert container is refused
by the gesture rather than drained; see the limits below.

### The dose

The operator's client no longer builds the syringe minigame itself and no longer needs a dose constant:
it runs the item's OWN `useLimbAction(displayLimb, item)` inside the existing medical capture scope, and
the new `WaterContainerItem.Inject` prefix diverts that native call — it reports the ml the native
delegate just computed to the host-authoritative session and leaves the local drain and effect to the
host/target. The unchanged native minigame keeps the item's real rate, its color and its own behaviour
for one-shot items (a bloodbag's delegate injects without a minigame, so the session ends on the spot).

The host still owns consumption: it builds the drain plan itself (proportional across stacks, capped at
the remaining total — `WaterContainerItem.CalculateDrain`'s own math, no per-item constant) and drains
the authoritative item snapshot exactly as before.

### The effect

The host no longer computes or applies a per-ml effect. Each accepted delta's drained plan rides the
wire to the target, and the target's own client applies it to its own limb through the native path:
`Liquids.Registry[liquidId]` → `injectionSickness` sickness and blood viscosity, then `injectable` →
the liquid's own `onHealthUse(amount, limb)`. The affected side therefore runs exactly the code the
game runs for a local injection, including the `CoUtils.DoTimedOp` timed bodies, which accumulate
per delta the way the native per-frame calls do. The target re-reports its character immediately after
applying a dose (the same immediate re-report the receiving side already sends), so the host's snapshot
and every observer catch up through the ordinary paths.

For the migrated family the target's own body is driven by the doses it applies, not by the echoed
medical snapshot: the host's copy of a guest body has always been report-driven, and overwriting a
locally applied native effect with a one-round-trip-old echo would fight it. The same argument covers
the two display sinks (`CloneFactTable.ApplyMedicalState` and
`RemoteMedicalCoordinator.ApplyMedicalState`) — neither has a staleness guard, and this message's
`TargetHealth`/`TargetLimbs` are the target's last report, a snapshot from BEFORE the dose the same
message carries, so feeding them would replace the display's live values with pre-injection ones on
every delta. On this family the display advances from the target's own reports instead. The other
families (shrapnel, bandage, AED, amputation, dislocation, suture, removals) keep the host-applied
snapshot untouched.

### What Part A deleted

- `RemoteMedicineCatalog` (item amounts, liquid effects, timed ids, inert list) and
  `RemoteMedicineApplication` / `RemoteMedicineLiquidEffect` with it;
- the injection half of `TimedBodyEffectApply`'s switch — the effect ids only the injection table could
  produce no longer travel as `TimedBodyEffectMsg`;
- `MedicalOperationEndCommittedMsg.TimedBodyEffects`, which had no producer left;
- the `IsInjectableItem` / `IsSupportedMedicineLiquid` / `TryGetInjectionAmount` call sites in
  `PlayerItemUseService`, `LocalUseItemEligibility`, `RemoteMedicalOperationHandler`,
  `InjectionStartValidator` and `MedicalOperationInjectionApplier`.

### Hard acceptance

Delete the constant table and every existing case of the chain stays green. The chain's case list is the
one the inventory produced: `MedicalOperationSessionServiceTests`, `MedicalToolApplicationTests`,
`InteractionGateAuthorityTests`, `MedicalOperationConcurrencyTests`, `ShrapnelOperationApplicationTests`,
`MedicalTargetBodyValidatorTests`, the medical rows of the direction and adaptive-stream tests, and
`RemoteMedicalDisplayProjectionTests`. Cases that pinned the *table* or the host's own effect
computation are rewritten to pin the same user-visible outcome through the new mechanism, and the
rewrite is listed in the self-check rather than done silently; no case was dropped.

## Part B — the remaining chains (not started)

The same shape, one chain per deliverable, each with its own hard acceptance:

| Chain | Table today | Native predicate and path |
|---|---|---|
| Topical (dressing/ointment) | `RemoteTopicalCatalog` | `healthUsable` liquid, `ApplyToLimb(limb, amount)`; the per-item amount is again a literal in the delegate |
| Eat / drink | `RemoteConsumeCatalog`, `RemoteDrinkMedicineCatalog` | `ItemInfo.useAction` → `WaterContainerItem.Drink` → `LiquidType.onDrink`, and the solid-food branch |
| Wear | `RemoteWearCatalog` | `wearable` with `desiredWearLimb` / `wearSlotId` |
| Limb tool | `RemoteLimbToolCatalog`, `RemoteHealProfiles` | non-null `useLimbAction` plus the tool's own tag |

Each of these also decides whether the item-level gesture routing can stop being a table (Part A left
the routing itself table-driven: the operator picks the injection family by "usable on a limb and holds
an injectable liquid").

## Red lines that do not change

- The allowed operation set, resource consumption and first-writer-wins arbitration stay host-side.
- The affected side judges on its own picture and timeline; latency never becomes a judgment input.
- Compatibility is not a design input: the tables are replaced, never kept alive beside the predicates.

## Limits recorded with Part A

- The dose call count on the target follows the ACCEPTED DELTA cadence (the operator's per-frame reports
  coalesced by `MedicalInjectionReportBuffer` and the adaptive interval), not the operator's frame count.
  Linear effects are unaffected; `WaterContainerItem.Inject`'s per-call, ml-independent
  `bloodViscosity -= injectionSickness * 0.1f` term and any per-call random roll therefore follow the
  message cadence. For every vanilla injectable except `biochem` (`injectionSickness = 2f`) that term is
  zero.
- A mod liquid can declare `Injectable` (`ModLiquidDefinition.Injectable`) while the mod API has no way
  to give it an `onHealthUse` delegate — that is Part 3 A of the ceiling ticket. The migrated target-side
  path logs and skips an injectable liquid with no delegate instead of calling a null delegate the way
  native `WaterContainerItem.Inject` would.
- The operator-side entry is only as native as the delegate: an item whose `useLimbAction` never reaches
  `Inject` (the topical containers) produces no dose and is refused by the routing predicate before the
  session starts.
- The routing predicate is stricter than the native dispatch in ONE case, recorded rather than hidden: an
  entirely inert container (a syringe filled with plain water) is refused, because CUO cannot read which
  native call an item's delegate makes and needs the `injectable` stack to keep the topical family out of
  this chain. Native `PlayerCamera.ApplyWoundItem` would run the delegate and drain the water for no
  effect. Mixed containers (an injectable plus a solvent) are admitted and behave natively.
- The host's copy of a guest target no longer advances with each delta — it advances from the target's own
  reports, like every other state of a guest body. The consequence for a viewer is that the patient's
  vitals move at the report cadence (one immediate re-report per applied dose, then the ordinary 1 Hz
  path) instead of the host pushing a locally computed value per delta. The target itself sees each dose
  immediately, which it did not before.
- When the target cannot apply a dose it has already received (a liquid the game's registry does not know,
  or a body with no usable limb), the host has already committed the ml and drained the item: the dose is
  logged and skipped on that side, so the resource is spent without an effect. The alternative — refusing
  the whole operation — would need the host to know the game's registry, which is the table this Part
  deleted.

## Open questions

- Does the third-party view stay acceptable at report cadence? Part A left the observer path on the
  ordinary character sync; the acceptance batch judges it.
- Should the remaining chains share one "native limb action" operator entry (one patch on `Inject` and
  `ApplyToLimb` and `Drink`) instead of one per chain? Decide when the second chain is cut.

## Non-goals

- Not a mod-API surface change: Part 3 A/C of the ceiling ticket stay parked until a real consumer.
- Not a rewrite of the operation-session protocol: the session envelope, claims and terminal semantics
  are reused as they are.
