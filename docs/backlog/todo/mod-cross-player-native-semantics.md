# Cross-player semantics from the game's own data

- Status: Todo — **cut 2026-10-08** out of `mod-content-ceiling.md` Part 2 stage 2, which required this
  stage to become its own architecture ticket. Part A (the injection chain) landed in the same cycle;
  Part B's topical chain landed 2026-10-08 as well, and so did its consume (drink) chain, so the ticket
  carries the two remaining chains plus the solid-food branch, which was cut to its own ticket when the
  drink chain landed.
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

## Part B — the topical chain (landed 2026-10-08)

The dressing/ointment chain, migrated in the same shape as Part A. Self-check:
`docs/evidence/selfchecks/players/cross-player-native-topical-semantics-selfcheck.md`.

### The predicate

`TopicalAdmission` is `InjectionAdmission`'s mirror over the same
`ILimbUseSemantics` seam: the item's own data says it is a limb-drawable liquid
container, AND at least one of its stacks holds a `healthUsable` liquid — the
single flag native `WaterContainerItem.ApplyToLimb` gates the liquid's own
`onHealthUse` on. The id table and both per-ml dictionaries are gone, so every
vanilla container the table never carried and every mod one qualify. The two rules
decline each other's family (verifiable against the game only by hand: six
`healthUsable` liquids, twenty-six `injectable`, no overlap — the readable pin is
the gate's carrier census), so what keeps a container holding BOTH kinds on one
chain is the order the routing sites ask them in, injection first.
`ItemUseTests.Use_AContainerHoldingBothKinds_IsRefusedByTheInjectionFirstOrder`
pins that order at a production site, and
`TopicalSemanticsTests.TheTwoRulesAreDisjointOnTheContentThisSuiteModels` pins only
that each rule declines the other's family.

### The dose

`RemoteTopicalUseHandler.TryMeasure` runs the item's OWN `useLimbAction` inside
the existing `NativeLimbActionScope`, and the new
`WaterContainerItem.ApplyToLimb` prefix diverts that call: the ml the delegate
computed (an `ldc.r4` literal — 10 for paincream, disinfectant and spraybottle,
20 for woundglue) becomes the request's dose and the native call is swallowed, so
the operator's own item is never drawn and the displayed copy is never mutated.
Unlike the syringe family the vanilla topical delegate is one synchronous call,
so the capture lives for the length of that call rather than a session.

All three operator entries measure: the medical view's limb gesture (on the
displayed limb), the world drag onto an in-world player (on that player's own
render clone, falling back to the camera's selected limb, with the limb hint left
at -1 so the patient still auto-picks), and the held-remote-item route (on the
requester's own selected limb — the limb the replaced native call reads — with
the amount riding the existing `RemoteInventoryIntentMsg.Amount`).

### The effect

The host builds the drain itself with `LiquidDrainPlan` (the native
`CalculateDrain` shape, capped at what the authoritative item really carries),
commits it and writes NO target state. The drained plan and the operator's limb
ride `PlayerItemUseResultMsg.AppliedDose` / `.LimbSelection` and the journal
event behind it; the patient's own client resolves the limb against its own body
(`NativeLimbTarget`, shared with the injection chain) and runs each liquid's own
`onHealthUse` through `NativeTopicalApply`, which is `ApplyToLimb`'s per-stack
loop minus the drain.

Two consequences worth naming, both shared with Part A: the host's copy of a
guest target advances from the target's reports rather than per delta, and the
result carries no host-computed body snapshot for this family.

One deliberate difference from Part A, with its reason: the patient's apply runs
OUTSIDE the `RemoteApply` scope, because `SoundPlayPatch` refuses to relay any
clip played inside it and a topical container's liquid clip comes from
`onHealthUse`. Keeping it inside would have removed a clip every peer hears
today. The injection chain's equivalent tail is left as it stands.

### What Part B deleted

- `RemoteTopicalCatalog` (4 item ids, 6 liquids with per-ml coefficients, the
  pain multiplier), `RemoteTopicalApplication`, `RemoteTopicalLiquidEffect`;
- the treatment-sound table's four topical rows (`disinfectant` and `spraybottle`
  from `TreatmentClips`, both cream rows and the `LiquidDrivenItems` list): both
  halves of a topical clip are native now;
- `RemoteMedicalOperationHandler.PlayTreatmentSound`'s liquid-clip half, which had
  no other client left.

### Hard acceptance

Delete the constant table and every existing case of the chain stays green. The
three `ItemUseTests` topical cases are rewritten in place to pin the same
user-visible outcome through the new mechanism — the dose instead of the host's own
per-ml math, the limb selection carried instead of resolved, the registries'
verdict instead of an allowlist. The deleted catalog's 7 cases are not all
replaceable and the self-check's §6 lists them one by one: four have a successor
(three superseded in a stronger form, one replaced), and **three have none by
construction** — they pinned CUO's transcription of the game's per-ml arithmetic,
which is now the game's own delegate executed on the patient's client and cannot be
reached from an L0 test. They are named rather than counted as replaced, and none
was dropped silently.

## Part B — the consume (drink) chain (landed 2026-10-08)

Everything a player drinks, migrated in the same shape as Part A and the topical chain. Self-check:
`docs/evidence/selfchecks/players/cross-player-native-drink-semantics-selfcheck.md`. It covers both
tables the row below used to name: the water/food containers `RemoteConsumeCatalog` carried and the
drinkable medicines `RemoteDrinkMedicineCatalog` carried — they are the same native call, and the
difference between water and naltrexone is only which liquid the container holds.

### The predicate

`ConsumeAdmission` asks a new `IConsumeSemantics` seam (Runtime, answered by the Game Adapter over
`Item.GlobalItems`): the item's own data says the game can USE it as a liquid container — its
`ItemInfo.Stats` is a `LiquidItemInfo` whose `usable` flag is set, which is exactly the gate
`Body.UseItem` applies before running the item's own `useAction` — AND it still holds liquid. The LIQUID
decides nothing: native `WaterContainerItem.Drink` has no liquid-level gate at all (unlike `Inject`'s
`injectable` and `ApplyToLimb`'s `healthUsable`), it drains whatever the container holds and runs each
liquid's own `onDrink`, so a liquid the deleted 14-row allowlist never carried is the game's own case
rather than a reason to refuse.

The limb rules are asked BEFORE this one at every routing site, and that order is what keeps the
containers the game marks both ways (`saline`, `ringersolution`, `bloodbag`, `bloodbaghuman` — `usable`
AND `usableOnLimb`) on the medical family, exactly as they behaved before this chain migrated. The two
rules are not disjoint, which `ConsumeSemanticsTests` states and
`ItemUseTests.Use_ADrinkableInjectableContainer_IsRefusedByTheInjectionFirstOrder` pins where it is
observable.

### The dose

`RemoteDrinkUseHandler.TryMeasure` runs the item's OWN `useAction` and the new
`WaterContainerItem.Drink` prefix diverts that call: the ml the delegate computed (an `ldc.r4` literal —
100 for a water bottle, 20 for naltrexone, 5 for sleeping pills) becomes the request's dose and the
native call is swallowed, so the operator's own item is never drawn and the liquids' `onDrink` bodies
never run there. The delegate is handed the AFFECTED player's own body, because an item's use action can
READ the body it drinks from — mindwipe's item-level health gate is the vanilla instance — and only that
player's own picture may answer it. All three operator entries measure: the world drag (on the affected
player's render clone, refused by name when it is not rendered), the held-remote-item route (on the
requester's own body, with the ml riding the existing `RemoteInventoryIntentMsg.Amount`), and the
wound-view release a drink container can reach.

### The effect

The host builds the drain with `LiquidDrainPlan` (the native `CalculateDrain` shape, capped at what the
authoritative item really carries), commits it and writes NO target state. The drained plan rides
`PlayerItemUseResultMsg.DrinkDose` and the journal event behind it; the patient's own client runs
`NativeDrinkApply`, which is `Drink`'s per-stack loop minus the drain: each liquid's own
`onDrink(ml, body)` inside the item-use sound scope, so the clips those delegates play are relayed
exactly as a local drink's are. The timed bodies (`CoUtils.DoTimedOp` for antirad, naltrexone,
braingrow), the component doses (sleeping pills, antidepressants, the mindwipe script) and the per-call
random rolls therefore all start on the body they land on, with no CUO message in between.

### What Part B's drink chain deleted

- `RemoteDrinkMedicineCatalog` (12 item amounts, 13 per-ml liquid effects, the mindwipe mirror),
  `RemoteDrinkMedicineApplication`, `RemoteDrinkMedicineEffect`;
- `RemoteConsumeCatalog`'s 14-liquid table and its `DrinkAmountMl` constant,
  `RemoteConsumeApplication`'s drink plan and effect, `RemoteLiquidEffect`;
- the whole `TimedBodyEffectMsg` chain — `PlayerItemUseResultMsg.TimedBodyEffects`,
  `PlayerInteractionTimedBodyEffect`, `WirePlayerInteractionTimedBodyEffect`, `TimedBodyEffectApply` and
  its four remaining branches — which had no producer left once the last timed medicine effect became a
  liquid's own delegate.

### Hard acceptance

Delete the constant tables and every existing case of the chain stays green. The two deleted catalogs'
22 cases are dispositioned one by one in the self-check's §6, because "no case was dropped" is not true
as a blanket statement: eight have no successor by construction (they pinned CUO's transcription of the
game's per-ml arithmetic, which is now the game's own `onDrink` executed on the patient's client and
cannot be reached from an L0 test), and the other fourteen are superseded in a stronger form, rewritten
in place or kept unchanged.

## Part B — the solid-food branch (cut to its own ticket)

`todo/mod-cross-player-solid-food-semantics.md`. Its native shape is not this one: a food item's
`useAction` is a delegate that calls `body.Eat` / `body.Drink` and writes body fields and the item
directly, with no divertible container call and no data field carrying the amounts — so the affected
side cannot run it without an item instance. The `Food` half of `RemoteConsumeCatalog` still answers for
it, and the ticket carries the two candidate designs and their trade-offs.

## Part B — the remaining chains

The same shape, one chain per deliverable, each with its own hard acceptance:

| Chain | Table today | Native predicate and path |
|---|---|---|
| Wear | `RemoteWearCatalog` | `wearable` with `desiredWearLimb` / `wearSlotId` |
| Limb tool | `RemoteLimbToolCatalog`, `RemoteHealProfiles` | non-null `useLimbAction` plus the tool's own tag |

Each of these also decides whether the item-level gesture routing can stop being a table (Part A left
the routing itself table-driven: the operator picks the injection family by "usable on a limb and holds
an injectable liquid"; this chain added the item's own `usable` flag beside it).

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

## Limits recorded with Part B's drink chain

The full list is in the self-check; the ones a reader of this ticket should not
have to go looking for:

- **An item in the drink class whose delegate is not a drink runs its own action
  on the operator's client and is then refused.** The class is the game's own
  flag and nothing in the data says which native call a delegate makes, so the
  measurement finds out by running it. The measurement sites ask the injection rule
  first (`LocalUseItemEligibility.FamilyOf`), so the two vanilla blood bags — whose
  `useAction` is `Item.DrawBlood`, a real fill of the operator's bag and a real
  drain of the treated limb — are never measured and are refused by name exactly as
  before this chain migrated (the independent review's blocker was that this order
  was missing at the measurement sites while the host, the eligibility gate and the
  medical handler all had it). What remains exposed is `liquidcentrifuge`: a usable
  `LiquidItemInfo` whose `useAction` runs the centrifuge, reachable from BOTH the
  world drag and the wound-view release, so dragging it onto a teammate separates
  the operator's own centrifuge contents before the use is refused. A mod item in
  that state is exposed the same way; the alternative is the hand-written "which
  usable containers are drinks" list this ticket exists to delete.
- **The mindwipe health gate moved from the host's mirror to the operator's own
  native run.** It reads the drinking body's vitals, which belong to the patient,
  so it runs on the operator's client against the affected player's displayed
  body; the host sees a dose or no dose. A modified operator client can therefore
  assert a dose for a target its own copy calls healthy — the trust boundary the
  dose VALUE already has, and unavoidable once the gate's inputs became the
  patient's own picture.
- **The world-drag measurement refuses when the affected player's render clone is
  not rendered on the operator's client.** The clone IS the body the item's own
  use action is handed, and an item delegate may read it, so answering from this
  client's own body would decide a remote fact from local state. The topical limb
  measurement keeps its own fallback, because there the body argument is a clip's
  position rather than a gate's input.
- **Two eligibility changes are deliberate and user-visible**, both from asking
  the item's flag instead of the deleted liquid allowlist: a limb-usable container
  the game cannot USE (`spraybottle`, `syringe`) refilled with a drinkable liquid
  is no longer fed to a teammate and now falls through to the native drop, and
  the containers the game marks both ways stay refused on the one-shot path by
  the routing order, as before.
- **The native drink's trailing item-level clip is not replayed** (the `sound`
  argument `Drink` plays at the end); no cross-player drink clip played before
  this chain migrated either. The liquid-level clips some `onDrink` bodies play
  are native now and are relayed from the patient.
- **The dose's VALUE is still the operator's own client assertion**, capped by
  the host at what the authoritative item carries — the same trust boundary the
  topical chain records.

## Open questions

- Does the third-party view stay acceptable at report cadence? Both migrated chains leave the observer
  path on the ordinary character sync; the acceptance batch judges it.
- ~~Should the remaining chains share one "native limb action" operator entry?~~ **Answered when the
  second chain was cut.** The patch must stay per native call — `Inject` and `ApplyToLimb` are
  different methods and a divert has to bind the one call the delegate actually reaches — but the
  operator-side shape is now shared: `NativeLimbActionScope` around the item's own `useLimbAction`,
  plus one divert member per native call. What the chains do NOT share is where the captured amount
  goes, and they should not: the injection family owns a sustained session on the medical-operation
  wire (start/update/end, reservations, a minigame), while the topical family measures one synchronous
  call that rides the one-shot use request. Each remaining chain decides for itself, and `Drink` is a
  third native call on a third wire family.

## Limits recorded with Part B

The full list is in the self-check; the ones a reader of this ticket should not
have to go looking for:

- **The sound scope is the one deliberate difference from Part A.** A topical
  container's liquid clip comes from `onHealthUse`, which runs on the patient, and
  `SoundPlayPatch` refuses to relay anything played inside the `RemoteApply` scope
  — so the patient's apply runs outside it, in its own `CharacterMedicalUse`
  scope, and the clip is relayed exactly as it is for a local application. Only
  `Sound.Play` keys off that scope; Part A's injection tail is left as it stands.
- **The measurement is one synchronous native call.** A delegate that deferred the
  container call would measure nothing and the gesture is refused by name rather
  than reported as a zero dose. Unreachable today: a topical container is a
  vanilla `LiquidItemInfo` whose delegate is one `ApplyToLimb` call, and the mod
  API cannot author a limb action at all (Part 3 A).
- **The routing reads the liquid, the native dispatch reads the item.** A
  container holding a `healthUsable` liquid whose delegate called `Inject` would
  be admitted here, measure nothing and be refused — with the native `Inject`
  having already run locally. No vanilla item is in that state and mod content
  cannot author one yet; the refusal is logged and no host drain is committed.
- **The world-drag entry has no limb of its own**, so it measures on the remote
  player's render clone (falling back to the camera's selected limb) and still
  sends the -1 hint, leaving the treated limb to the patient. The amount is
  limb-independent for every delegate that can reach this chain today.
- **A dose the patient cannot apply** (an unknown liquid, a `healthUsable` liquid
  with no delegate, a body with no usable limb) is logged and skipped after the
  host has committed the ml: the resource is spent without an effect.
- **The dose's VALUE is the operator's own client assertion.** What is captured is
  the amount argument the item's delegate passed — a per-item constant (10 or 20)
  for this family — not what the container holds or would drain. The
  truth-preserving step is the host's cap, `LiquidDrainPlan`'s
  `Math.Min(amount, total)` over the authoritative stacks, so a client can ask for
  no more liquid than the item really carries but it does choose the amount. Part
  A's limits discuss the dose call COUNT; this is the same trust boundary on the
  other family.
- **One eligibility change is deliberate and user-visible.** The world drag's
  `IsUseItem` used to refuse a topical bottle whose liquid was not one of the
  deleted table's six, and the generic tail that followed would otherwise have
  accepted it as a DRINK; with the table gone that bottle now falls through to the
  drink branch, so dragging, say, a water-filled spraybottle onto a teammate feeds
  them instead of dropping the item. That is the HOST's own behaviour for any water
  container, and was already the behaviour for a syringe of water under the old
  gate — the id table was the anomaly, not the rule — so the operator gate now
  agrees with the chain it feeds.
- **The journal dedupes the result by operation id**, so the ordinary missed-range
  replay cannot double-apply the dose. The residual window is a re-delivery after a
  checkpoint `Restore`, which clears that set: stated precisely, it is unreachable
  from any current sender unless a mid-session restore rolls the kernel back behind
  batches already applied. A real session must settle it; a dose is the first
  member of this family for which that matters, every other effect it carries being
  idempotent.
- **Coverage direction:** every L0 topical case is the guest-operator direction. The
  host-operator direction shares the same host entry and the same patient apply but
  no case drives it, and no case drives a third peer — both are acceptance-batch
  rows.

## Non-goals

- Not a mod-API surface change: Part 3 A/C of the ceiling ticket stay parked until a real consumer.
- Not a rewrite of the operation-session protocol: the session envelope, claims and terminal semantics
  are reused as they are.
