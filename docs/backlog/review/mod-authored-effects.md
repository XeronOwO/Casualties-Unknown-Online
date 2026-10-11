# Mod-authored effects: the code registration face and the operation surface it writes through

- Status: Review — **promoted 2026-10-08 by the user's directive**, which supersedes the ceiling ticket's
  Stage 3 gate. The gate said "only if stage 2 leaves a real need ... trigger is a second real consumer";
  the user's ruling is that a highly customisable effect surface is the point of the mod platform, so the
  consumer count is no longer the trigger. The old gate is recorded as SUPERSEDED (not deleted) in
  `mod-content-ceiling.md`'s Stage 3 paragraph, with this ticket as its successor. **Order:** it follows
  `mod-content-typed-registration.md`, which retypes the content contract this surface registers into.
  **Landed 2026-10-11** (see *What landed*): the census, the contract, the Runtime rails, the Game Adapter
  implementation, the docs and the decisions are in, and what remains is the acceptance batch's own row —
  a live session is the only thing that can judge whether the effect lands on the right body and the right
  limb, and L0 builds no live `Body`.
- Priority: High (user-promoted)
- Category: Mod platform / mod API
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 2 Stage 3 and Part 3.A's effect gap)
- Related: `docs/backlog/review/mod-declared-behaviour-with-no-function.md` (the cycle that makes a
  declaration without a function non-fatal, and whose inert default is this ticket's no-effect-registered
  path), `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md` (the tiers and the
  promotion funnel), `docs/decisions/active.md` (184: the affected side judges on its own picture),
  `docs/en/reference/modification-policy.md` (the `Abstractions` no-game-type rule)
- Source: the user's 2026-10-08 rulings, in their own words: "既然做了抽象层，开放了抽象接口，为什么不把可能用
  到的功能都做成接口 api 下放呢？你就应该考虑到，有玩家想拓展高度自定义的效果", and then "可以将 GameObject
  这种基础类型通过接口属性暴露出来，假如有实力的开发者就想获取游戏原生对象，他就用 as 去取" (the
  engine-typed hatch, design point 4)

## Why this ticket exists

A mod liquid can declare `HealthUsable` / `Injectable`, but the API cannot carry the function those flags
gate, and the game calls it unguarded — today that is a crash, and
`mod-declared-behaviour-with-no-function.md` makes it a defined, inert outcome. That ticket deliberately
stops at "no function is expressible". This one is the other end: a mod SHOULD be able to author the effect,
and the platform should hand it enough to write one.

## The design this ticket is built on (decided, not open)

1. **The effect is registered in CODE, keyed by content id.** Content travels as an opaque payload to every
   peer; a function is not data and cannot ride it. What travels stays the content's data (id, colour, value,
   flags, qualities); the behaviour is code each peer already has, looked up by the id it was registered
   under. That is also what makes the cross-player families work with no wire change: they already send the
   liquid id and the amount, and the affected player's own client runs the game's own delegate.
2. **The callback stays CUO-typed.** `Abstractions` keeps its rule — no game assembly, no Unity type — so the
   effect's signature names CUO's own context, never `Body` / `Limb` / `ItemInfo`. The registration face
   lives in `Abstractions` (a mod registers through `IModContext`), and the Game Adapter is what bridges it
   onto the vanilla delegates.
3. **The primary path is NAMED OPERATIONS.** What the mod may write is reached through CUO's own named
   operations, grown from the game's own effects by the census below, so the write set is enumerable,
   documented, permission-checkable and stable across a game update.
4. **An ENGINE-typed escape hatch sits beside them** (user ruling 2026-10-08: "可以将 GameObject 这种基础类型
   通过接口属性暴露出来，假如有实力的开发者就想获取游戏原生对象，他就用 as 去取"). The effect context
   carries the engine object the effect landed on, typed with the ENGINE's own type (`GameObject`) — never a
   game type, so the contract's surface does not churn with the game; only the mod's own `as` /
   `GetComponent` cast can, and that is the native-binding risk the mod already declares. It is usable
   rather than decorative: `Body`, `Limb` and `Item` are each `MonoBehaviour`
   (`reversing/Assembly-CSharp/Assembly-CSharp/` — `public class Body : MonoBehaviour`, `Limb.cs`,
   `Item.cs`), so the object recovers them; the plain-class content (`ItemInfo`, `LiquidType`) needs no
   handle at all, because it is reachable by id from the game's own registries.
   - The rejected shape is the UNTYPED hole (`object` as the surface): it cannot be documented, cannot be
     type-checked, and says nothing about what it is. An engine type says what it is.
   - The hatch's own rules, written into the contract: it is the object of the state THIS client owns
     (decision 184 — a fact about another player is not read here), Unity null semantics apply (`== null`),
     and a mod that uses it is outside the API's promise and declares a `NativeBinding`.
   - **The payload side does not change**: registrations and content definitions stay data-only (no Unity
     type, no game type), because they travel to every peer. The repository already draws the line where
     this needs it — only `Assembly-CSharp` is reserved for the adapter, engine modules are allowed
     (`GameAssemblyReferenceGateTests`: "every other framework project compiles against the adapter's ports,
     the Runtime and the engine modules instead of the game's own code") — and `Abstractions` simply carries
     no engine reference today, so adding one is this ticket's reviewed change to the contract's
     dependencies, with the docs updated in the same change.
5. **The effect runs on the client that owns the affected state** (decision 184): the liquid chains already
   run the liquid's own delegate on the affected player's client, so a registered effect lands there with no
   new message.
6. **Permission and consistency have their own answers.** Writing another player's body is a new capability
   class: it needs a `ModPermission` gate of its own, and the handshake's existing mod-id / version /
   `NativeBinding` parity is what makes "same content, same code" true across peers. Both get designed in
   this ticket, not assumed.

## First deliverable (before any code): the operation census and the interface shape

The exposure list is evidence, not taste. Step 1 is to read every vanilla liquid effect the game ships —
each `onDrink` / `onHealthUse` body in the game's own liquid registry — and census exactly which writes
they perform: hydration, temperature, happiness, sickness, blood viscosity, the component doses (opiate,
painkillers, medication scripts), the timed bodies (`CoUtils.DoTimedOp`), limb state (disinfect, pain,
muscle health, bleeding), the per-call random rolls and the clips. Step 2 turns that census into the
operation list plus the effect context (which liquid, how much, which target, which limb when the native
call has one), and step 3 brings the shape to the user for approval as this ticket's own scope. Only then
does code get written.

The item half (`useAction`, and the limb action Part 3.A lists) is the declared follow-on, not this stage;
the census decides how much of it the same operation surface already covers.

## The census (measured 2026-10-11, re-counted 2026-10-11 in review; `reversing/Assembly-CSharp/Assembly-CSharp/Liquids.cs`)

The registry is **73** liquids: 73 `new LiquidType` constructions, 73 `dictionary[…] = …` assignments and
73 distinct `localeName` values, counted those three ways because the first count of this census said 72
and was wrong (it silently skipped the one entry keyed by a bare variable name rather than a literal).
Every liquid carries an `onDrink`; 32 carry an `onHealthUse` (26 gated by `injectable`, 6 by
`healthUsable`). `Liquids.cs` is the WHOLE surface: a name-by-name sweep of the 264 decompiled
`Assembly-CSharp` files finds the two delegates assigned nowhere else — but it is not the whole set of
CALL SITES, which is one row further down. Reading all 105 delegate bodies yields this write set, which is
what the operation list below is derived from and nothing else.

| Group | Writes found | Where |
|---|---|---|
| Native body actions | `Drink(ml)`, `Eat(hunger, weightGain)`, `Vomit()`, `Ragdoll()`, `Burp()`, `TryStartFibrillation(true)` | `Body.cs` own methods; `vomiter.Vomit()` on the component |
| Body vitals | thirst, temperature, happiness, sickness, weightOffset, stamina, energy, consciousness, adrenaline, shock, caffeinated, dirtyness, brainHealth, bloodOxygen, bloodViscosity, bloodVolume, internalBleeding, strokeAmount, septicShock, radiationSickness, venomTotal, antibioticImmunityTime, clawRegrowTime, brainGrowSickness, heartRate, fibrillationProgress, bloodPressureChangeFromMedicine, respiratoryRate, stimulantMultiplier, miscShakeIntensity, overdoseIndex — plus hunger, which no liquid touches directly and `Body.Eat` writes | 31 fields read-modify-write or assigned outright, and `hunger` through the action |
| Body reads | `alive`, `standing`, `inCardiacArrest` | the guard conditions (`braingrow`, `oxylinedrink`, `epinephrine`). `conscious` is NOT one of them: `.conscious` appears zero times in the file (the ten matches are `.consciousness`) |
| Doses | `Painkillers.opiateAmount` / `.antagonistAmount` / `.opiateTolerance`, `Antidepressants.TakeDose`, `SleepingPills.amount` | eight liquids |
| Timed bodies | `CoUtils.DoTimedOp(key, op, seconds)` with `DurationOf` / `HighestDurationOf` read INSIDE the tick (the overdose tiers of all three stimulants and `antirad` / `epinephrine`; `braingrow`'s tick reads `alive`, not a duration) | 24 call sites, 10 duration reads |
| Sounds | `Sound.Play(clip, position, …)` — `pills`, `cream`, `syringe`, `drink` | 9 call sites |
| Limb vitals | pain, muscleHealth, skinHealAmount, infectionAmount, bandageSlowAmount, plus skinHealth (`bleach`) and bleedAmount (`procoagulant`, both halves) — the last two reached from a DRINK tick as well as the limb path, through `body.limbs[i]` | the `onHealthUse` bodies and two drink bodies |
| Limb operations | `SetDisinfect(ml)`, the `connectedLimbs` walk (antiserum disinfects every connected limb), and one rigidbody velocity add (`oxylinedrink`, only while the body is not standing) | `Limb.cs`, `Limb.rb` |
| Component attach | `MindwipeScript` added to the body (`braingrow`, over a dose threshold) | `Body.gameObject` |

The sites that RUN one of those delegates are five, not four. The game's own drink, CUO's cross-player
drink / topical / injection appliers and CUO's own custom-world-byte handler are the four the sibling
ticket mapped; `NonDescriptCan.Eat` is the fifth — it picks a weighted random id out of the whole
`Liquids.Registry` (skipping `Liquids.DangerList`) and calls `onDrink` on it, so a mod liquid is reachable
from the mystery can too. All five reach the same installed delegate, which is why the invoker resolves per
call.

Two shapes the census fixes that the API must reproduce rather than approximate: the timed body runs its
operation IMMEDIATELY and then once a game second, and `DoTimedOp` under an existing key only EXTENDS the
duration — the operation delegate stays the one first registered.

## Plan (this stage — the liquid effect, decided and approved 2026-10-11)

Scope approved by the user on 2026-10-11 from the census above: the full list, nothing deferred. The item
half stays the declared follow-on.

1. **Contract (`Abstractions`).** A registration face reached through `IModContext` — one delegate per native
   delegate, keyed by the liquid content id the mod owns — plus the effect context: which liquid, how much,
   whose body, the limb when the native call names one, and the engine-typed hatch. The write surface is
   named operations (`ModBodyVital` / `ModLimbVital` selectors with add/set/get, the native actions, the
   component doses, the timed body and the clip), never a game type. `Abstractions` gains a
   `UnityEngine.CoreModule` reference for exactly one member (`GameObject`), which is this contract's
   reviewed dependency change.
2. **Permission.** A new `ModPermission` bit gates registration, enforced at the registration call the way
   `IModContent` enforces `RegisterContent`; `All` gains it.
3. **Runtime.** A per-mod, permission-gated registration adapter over one framework-wide store keyed by
   liquid id, plus the resolver seam the Game Adapter reads. An effect may only be registered for a liquid
   the SAME mod registered as content of the liquid kind, and the store is what the resolver answers from, so
   the answer is live rather than captured at build time.
4. **Game Adapter.** The invoker the two vanilla delegates call: it resolves the registered effect, builds the
   CUO-typed context over the live `Body` / `Limb`, and invokes it inside a guard so a mod's exception costs
   the effect and not the game's frame. A liquid with no registered effect keeps today's inert reporting
   delegate. The operation implementations are the only place that touches the game's fields.
5. **Consistency.** No wire change: the five cross-player chains already run the liquid's own delegate on the
   affected side, and the handshake's id/version/permission parity is what makes "same content, same code"
   true. Documented, not assumed.
6. **Tests.** The registration rails and their refusals, the operation-to-field mapping (including the two
   shapes the census fixed: the immediate-first tick and the extend-don't-replace key), the guard, and the
   census reconciliation as a gate over the operation list.
7. **Docs.** `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` in the same change, the
   alignment record re-confirmed, the `Abstractions` baseline re-reviewed, a decision entry, and this
   ticket's own status.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Registration face | `IModContext` member + per-mod adapter | cases: no permission, unknown liquid id, a liquid another mod owns |
| Named operations → game fields | the mapping itself | one case per operation group asserting the field moved |
| Timed body | start/extend/read through the game's own `CoUtils` | the immediate-first tick and the extend-not-replace key |
| Guard | a throwing effect costs only the effect | case invoking a throwing delegate |
| No-effect path | the inert reporting delegate is unchanged | the existing declared-behaviour cases stay green |
| Census reconciliation | the operation list vs the game's own writes | gate case |
| Reach | end-to-end in a real session | acceptance row — L0 builds no live `Body` |

## What landed (2026-10-11)

- `Abstractions`: `IModLiquidEffects` (the registration face, reached through the new
  `IModContext.LiquidEffects`), `ModLiquidEffect` (the callback), `IModLiquidEffectContext`,
  `IModEffectBody`, `IModEffectLimb`, `ModBodyVital` (32 members) and `ModLimbVital` (7), the new
  `ModPermission.AuthorEffects` bit, and the contract's one engine reference
  (`UnityEngine.CoreModule`, for `GameObject`).
- `Runtime`: `ModLiquidEffectStore` (the framework-wide leaf, the same shape as `ModContentStore` beside
  it), `ModLiquidEffectsAdapter` (the per-mod permission/ownership/duplicate rails) and
  `ModContentStore.IsRegisteredAs` (the kind-aware ownership question the rail needs).
- `Game Adapter`: `ModEffects/` — `LiquidEffectInvoker` (what the two installed delegates call: resolve per
  call, build the context, invoke inside a guard), `ModLiquidEffectContext`, `ModEffectBody` and
  `ModEffectLimb` (the whole write surface, one mapping table each). The liquid provider installs the
  invoker's two delegates instead of the no-op pair, and reports a declared effect flag with no registered
  function when the liquid is injected into the game's registry.
- `tests/CasualtiesUnknownOnline.Tests`: `ModLiquidEffectRegistrationTests` (9 cases — the permission, the
  ownership rail in both directions, the kind rail, the duplicate rail, the live-lookup shape, the
  failed-load withdrawal), plus the four suites that build a Game Adapter content provider reflectively,
  now through one shared `GameAdapterProviderFactory`.
- `tests/CasualtiesUnknownOnline.NormativeGates.Tests`: `ModEffectOperationGateTests` — every declared
  vital has exactly one mapping arm, every arm reads and writes the SAME game field, and every declared
  permission bit is inside `ModPermission.All`.
- Docs: the `mod-api.md` pair carries the new section and the DTO/permission rows, the alignment record is
  re-confirmed at the new contents, the `Abstractions` baseline is re-reviewed and adopted, and decisions
  255/256 record the capability and the hatch.

## Self-check table (as delivered)

| Mechanism | Change | Evidence |
|---|---|---|
| Registration rails | permission, ownership, kind, duplicate, failed-load withdrawal | `ModLiquidEffectRegistrationTests` — 9 cases, driven over one store and one content store; the mutations below |
| Named operations → game fields | one mapping table per surface | `ModEffectOperationGateTests` (declared ↔ handled both ways, read field == write field); mutation-checked: deleting one arm and drifting one arm each turned it red |
| No-effect path | the invoker's own reporting branch replaces the provider's inline delegates | `DeclaredBehaviourContentTests` (3 liquid cases) stay green with the message shape unchanged |
| Permission vocabulary | the new bit is inside `All` | `ModEffectOperationGateTests.EveryDeclaredPermissionBit_IsInsideTheDefinedMask` |
| Public surface | the contract addition is the reviewed baseline | `ApiSurfaceGateTests` — the emitted candidate was reviewed and adopted as an EDITED update (95 added entries and ONE rewritten line, `ModPermission.All`, so no removal, no tombstone and no tombstone reordering: `git diff --numstat` reads `95  1`); the gate is green |
| Guard | a throwing effect costs only the effect, on the call AND on every tick of a timed body | source-level: the invocation is wrapped in `LiquidEffectInvoker`, and each tick in `ModEffectBody.Tick` — the tick guard is what keeps the game's own `CoUtils` coroutine (and the key it owns) alive; L0 cannot reach either, so this rides the acceptance row |
| Reach | end-to-end in a real session | acceptance row — L0 builds no live `Body`, so the invoker's guarded branch and the operation table are driven at the level the build sites reach |

## The independent review (2026-10-11) and what it cost

FULL-tier, fresh context, against the frozen tree, read-only; the report is
`.agent-local/reviews/mod-authored-effects-review.md` (gitignored). It found no blocker and four majors,
all four fixed in the same commit:

1. **The census said 72 liquids; there are 73.** Re-counted three ways (`new LiquidType`, `dictionary[…] =`,
   distinct `localeName`) after the review caught it — the first count silently skipped the one entry keyed
   by a bare variable rather than a literal. The wrong number had been copied into decision 255 and into the
   delivery checklist as well as here, and all three are corrected.
2. **The contract claimed the limb is never a framework substitute; on the injection entry it is.** CUO's
   injection applier resolves a request that names no limb with the game's own auto-select rule (the most
   injured attached limb) — a recorded, pinned rule, unlike the topical and limb-tool entries which refuse
   such a request (decision 246). The member's documentation now states that instead of denying it, and says
   the effect sees exactly what the game's own delegate sees.
3. **The "declared a flag with no function" report asked the wrong question.** It used "is this liquid
   registered at all", so a mod that registered only the DRINK half passed it while the player applied or
   injected the liquid with no effect and no line. The question is now `ModLiquidEffectStore.HasLimbUse`,
   named for the half the two flags actually gate, and the misleading general question is deleted rather
   than left beside it.
4. **A throwing timed tick was unguarded.** The call itself was wrapped but the tick was not, and the tick
   runs inside the game's own `CoUtils` coroutine: an exception would kill the coroutine while leaving the
   key and its remaining duration in the game's table, so the key would never tick again and every later
   call under it would only "extend" a body nobody was running. There is now a guard around each tick too.

Minors and nits it raised were fixed in the same pass: the stale `todo/` ticket path in four contract files,
the csproj comment that named only `GameObject` while `AddLimbVelocity` takes a `Vector2`, four census
inaccuracies (`conscious` is read by no liquid; `hunger` is written only through `Eat`; `braingrow`'s tick
reads `alive` and not a duration; `skinHealth` / `bleedAmount` come from the drink path too), a FIFTH call
site that runs an effect (`NonDescriptCan.Eat` picks a weighted random registry entry), the unreachable
`CoUtils.instance == null` branches (its getter lazily creates the singleton, so it never returns null), and
the `IModEffectBody.Conscious` member — the one read with no census support, and the native-API projection
already carries it for the local body, which is the only body an effect lands on, so it was removed rather
than documented into the rule.


## Limits (as delivered)

- The end-to-end half needs a live session AND a mod that registers an effect: the sample mod
  (`src/CasualtiesUnknownOnline.ModExample/`) declares no liquid content, and `ModEffectBody` /
  `ModEffectLimb` hold a live `Body` / `Limb`, which the test host cannot build. So the acceptance row
  needs a throwaway mod fixture for the batch. Named rather than replaced by a weaker check.
- The two game helpers this file replaces are named in `ModEffectBody`'s own doc comment: the dose
  components are reached with Unity's `GetComponent` / `AddComponent` instead of Unity.VisualScripting's
  `ComponentHolderProtocol`, which is not one of the referenced assemblies. The equivalence argument is
  that the game's own `body.TryGetComponent<Painkillers>` reads the same `GameObject`.
- A timed body extends rather than replaces, and the tick registered first keeps running — the game's own
  semantics, documented on the member, and a mod that wants a different one uses a different key.
- A mod may not author a vanilla liquid's effect: that is a content override, still parked in
  `mod-content-ceiling.md` Part 3.C.
- `ModPermission.AuthorEffects` is not one of the host/state bits a `ClientOnly` / `Cosmetic` mod is
  refused for, and that is deliberate rather than an omission: it grants nothing alone, because every
  registration is keyed by content of the mod's own and registering content already needs
  `RegisterContent`, which those modes ARE refused for. Such a mod may declare the bit and register
  nothing. Recorded on the enum member itself.
- No red was observed for the invoker or the operation tables: L0 cannot reach them, so their evidence is
  the source-level gate plus the acceptance row rather than a failure seen first. The same holds for the
  tick guard and for the load-time "declared a flag with no function" report, whose decision now lives in
  a named store question (`HasLimbUse`, covered by a case) but whose CALL SITE reads `Liquids.Registry`
  and therefore needs the game.
- `IModEffectBody.Limbs` skips a limb the game has already destroyed, so the list can be shorter than
  `body.limbs` while the indices it reports are unchanged — stated on the member rather than left to be
  discovered.

## Non-goals

- Not an UNTYPED game-object hole (`object` as the surface), and not a Unity or game type in any payload:
  the hatch is engine-typed and runtime-only (design points 4 and its payload half).
- Not the crash fix and the inert default — that is `mod-declared-behaviour-with-no-function.md`.
- Not a new wire family: the liquid chains already carry what the effect needs. If a later stage needs one,
  it takes its own message and its own protocol decision.
- Not the item half (`useAction`, `useLimbAction`) — the declared follow-on this stage's census was
  explicitly allowed to cover only part of.
