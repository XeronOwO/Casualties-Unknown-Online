# Mod-authored item actions: the use and limb functions, and what the item census left to the hatch

- Status: Done — **cut out of `mod-content-ceiling.md` Part 3.A on 2026-10-11** and landed in the same
  cycle (see *What landed*). It closes the follow-on two tickets declared: `mod-authored-effects.md` built
  the registration face and the named-operation surface for the LIQUID half only, and
  `mod-declared-behaviour-with-no-function.md` recorded the item half as "the follow-on that ticket
  declares" while noting that `Abstractions` CAN carry a function now. **Accepted 2026-10-11 in batch
  `20261011-k`** (`docs/evidence/acceptance/mod-item-authored-actions-20261011-k.md`): every row passes with
  no residual — the use action runs through the game's own `Body.UseItem`, the limb action through the wound
  view, and CUO's cross-player limb-tool chain runs the item's own delegate on the TREATED player's own
  client. The run also turned up one defect in the same build site that this ticket does not own (a mod item
  declaring no `Tags` throws in the game's own frames); it is filed as
  `docs/backlog/done/mod-item-with-no-declared-tags-throws.md` with the run's measurements.
- Priority: High
- Category: Mod platform / mod API
- Parent: `docs/backlog/done/mod-content-ceiling.md` (Part 3.A: `useLimbAction`, and the item half of the
  effect gap)
- Related: `docs/backlog/done/mod-authored-effects.md` (the liquid half — the same design, the surface
  this one extends), `docs/backlog/done/mod-declared-behaviour-with-no-function.md` (the cycle that made
  a declaration without a function non-fatal, and whose item report this one turns into "no action
  registered"), `docs/backlog/review/mod-item-wearable-declaration.md` (the sibling the same family audit
  found), `docs/backlog/review/mod-cross-player-native-semantics.md` (the cross-player limb-tool chain,
  which reads exactly the pair this surface installs), `docs/en/reference/mod-api.md`,
  `docs/en/reference/modification-policy.md`
- Source: the 2026-09-25 mod-ceiling inventory (Part 3.A) and the user's 2026-10-08 ruling that the
  platform should hand out every capability the abstraction layer can carry — in their own words: "既然做了
  抽象层，开放了抽象接口，为什么不把可能用到的功能都做成接口 api 下放呢？"

## Why this ticket exists

Part 3.A's item half is two defects of a surface the API already promises, seen from two sides:

- A mod item could declare `Usable` / `UsableWithLmb` and the game would run `ItemInfo.useAction` behind
  that flag — but the only functions expressible were `Tool` and `Gun` (built-in behaviours) or the
  reporting no-op `mod-declared-behaviour-with-no-function.md` installed. An author could say "usable" and
  not be able to say what using it DOES.
- `ItemInfo.useLimbAction` had neither an expressible function nor a flag: `IModItemDefinition` had no
  `UsableOnLimb` member at all. So the game's own wound view could never be reached by mod content, and
  CUO's cross-player limb-tool chain — which asks exactly `usableOnLimb` plus a non-null `useLimbAction` —
  could never carry a mod item either.

## The census (measured 2026-10-11, `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`)

`Item.cs` is the WHOLE surface: a name-by-name sweep of the 264 decompiled `Assembly-CSharp` files finds
`useAction` and `useLimbAction` assigned nowhere else. The file holds **195** assignments — **139**
`useAction` and **56** `useLimbAction`, with **16** `ItemInfo` instances carrying both — and reading all
195 bodies yields this write set, which is what the operation list below is derived from and nothing else.

| Write group | Members found | Carried by |
|---|---|---|
| Item state | `item.condition` (**78** use actions, **22** limb actions) and `item.decayMultiplier` (1) | the item field is the only ITEM state any delegate writes; `decayMultiplier` is a mechanism (the game itself uses it as a per-item decay switch), not an effect |
| Body vitals | happiness 39, sicknessAmount 24, energy 7, thirst 7, temperature 6, radiationSickness 5, brainHealth 5, caffeinated 5, attackCooldown 4, septicShock 3, bloodViscosity 3, consciousness 3, shock 3, internalBleeding 3, stamina 3, adrenaline 2, respiratoryRate 2, strokeAmount 2, bloodVolume 2, weightOffset 2, antibioticImmunityTime 2, dirtyness 2, and one each of hunger, bloodOxygen, brainGrowSickness, stimulantMultiplier, venomTotal, wetness, snowAmount, hearingLoss, eatTime, temporarySlowdown, usedNeuralBooster, usingSleepingBag, sleeping, moveForce, jumpSpeed, maxSpeed | `ModBodyVital` already carries all but thirteen. `Hemothorax` (`limb.body.hemothorax -= 35f`) is the one addition; the other twelve are hatch residents by the rule below and are named one by one there |
| Body actions | `Eat` 45, `Drink` 12, `Attack` 27, `Ragdoll` 3, `DropItem` 5, `PickUpItem` 7, `AutoPickUpItem` 3, `RemoveEye` 2, `TakeANap` 1, `DoGoreSound` 1, `vomiter` 4, `talker` 37, `SlotOf` 4 | `Eat` / `Drink` / `Ragdoll` / `Vomit` are already named operations; `Attack` is what the `Tool` behaviour is for; the rest are per-item mechanisms |
| Limb vitals (limb actions only) | pain 16, skinHealAmount 11, bandageSlowAmount 10, bleedAmount 7, dislocationTimer 6, boneHealTimer 6, muscleHealth 5, infectionAmount 3, skinHealth 3 | `ModLimbVital`'s seven plus the two recovery timers |
| Limb operations | `limb.SetDisinfect`, the `connectedLimbs` walk, `limb.CreateTemporarySprite` 13, `limb.transform` 9 | `Disinfect` / `ConnectedLimbs` are already named operations; the sprite is presentation |
| Engine / world | `Sound.Play` 83, `Resources.Load` 51, `Instantiate` 5, `Object.Destroy` 5, `WorldGeneration` 11, `FluidManager` 21, `AddComponent` 15, `GetComponent` 103, `transform` 111 (every figure counted over the same 195 bodies, with no per-prefix narrowing) | the engine hatch's, by the rule below |

### What this census decides, and the rule it applies

The liquid half's own census covered "every write the game's own shipped liquids perform". Applied
literally to the item half it would drag in movement parameters (`moveForce`, `maxSpeed`, `jumpSpeed`),
environmental fields (`wetness`, `snowAmount`), interaction modes (`sleeping`, `usingSleepingBag`) and the
whole world-facing tail — none of which is an effect a consumable has on a body. So the rule this ticket
applies, stated once and then followed:

> A member joins the named-operation surface when it is a **state quantity of a body, a limb or the item**,
> written as an accumulate-or-pin by the game's own content. Everything else — kinematics, environment,
> interaction modes, component lookups, object replacement, world writes — is engine work and belongs to
> the hatch.

By that rule the item census adds exactly three members: `ModBodyVital.Hemothorax`,
`ModLimbVital.DislocationTimer` and `ModLimbVital.BoneHealTimer`. Twelve further body fields the census
writes are deliberately NOT named operations, and are named here one by one so the enumeration is real
rather than implied: `attackCooldown`, `hearingLoss`, `eatTime`, `temporarySlowdown` and
`usedNeuralBooster` are per-item mechanism writes, while `wetness`, `snowAmount`, `sleeping`,
`usingSleepingBag`, `moveForce`, `jumpSpeed` and `maxSpeed` are environment, interaction modes and
kinematic parameters. Everything else the census found is already carried by the shared body/limb
accessors or goes to the hatch.

## The design (decided, not open)

1. **The registration face is the liquid half's, item-shaped.**
   `IModItemActions` reached through `IModContext.ItemActions`, one function per native delegate:
   `TryRegisterUse` (`ItemInfo.useAction`) and `TryRegisterLimbUse` (`ItemInfo.useLimbAction`), both
   `ModItemAction` — `(IModItemActionContext) → void`, one delegate for both halves, exactly as
   `ModLiquidEffect` serves the liquid half's two.
2. **Same permission, same ownership rail.** `ModPermission.AuthorEffects` (the "this mod authors
   behaviour" bit, not a per-kind one), the item's content registered first by the same mod as
   `ModContentKind.Item`, one function per half, refused rather than replaced.
3. **The item side has a reachability rail, and the liquid half now carries the same one on its limb
   side.** Both native item calls sit behind a flag the DEFINITION carries, so an action the game's gate
   can never reach is refused at registration rather than filed: the use half needs `Usable` — the flag BOTH
   call sites require, where `UsableWithLmb` only adds the left-mouse entry point — and the limb half needs
   the new `UsableOnLimb`. The same question was then put to the liquid half, where `onHealthUse` is gated
   by `healthUsable` / `injectable` (the drink call site by no flag at all), so `ModLiquidEffectsAdapter`
   gained the matching rail in the same cycle. The refusal is the reachability fact said out loud; the
   alternative — accepting it and reporting at load — would leave a registration no gesture can run.
4. **One item, one use action.** A definition that carries a `Tool` or `Gun` behaviour already HAS a use
   action, so the use half is refused for it. That keeps the entries of a gesture isolated: which function
   runs is decided by the definition's own data, never by which registration happened to run last.
5. **The write surface is shared, not duplicated.** `IModEffectBody` / `IModEffectLimb` are the same
   accessors a liquid effect gets — the game hands both families the same body and the same limb — and the
   item's own accessor (`IModEffectItem`) carries `Condition`, `AddCondition` (the plain field arithmetic
   the game's own items perform) and `SetCondition` (through the game's own `Item.SetCondition`, which
   scales a liquid container's contents to match).
6. **The engine-typed hatch reaches the item too**: `IModItemActionContext.NativeObject` is the item's
   `GameObject`, and `IModEffectBody.NativeObject` was added so the body's object has the same entry the
   limb's and the item's do. Same rules as the liquid half's hatch (this client's own object, Unity null
   semantics, `NativeBinding`).
7. **No wire change and no host change.** The registration is local on each peer's copy of the mod, and the
   affected side judges on its own picture and timeline (decision 184). Named in the design so it is
   recorded, not used as a reason: the cross-player limb-tool chain already runs the item's own delegate on
   the treated player's client, so a mod limb item becomes carryable across players with no message added.

## Plan (as carried out)

1. **Contract (`Abstractions`)**: `IModItemActions`, `ModItemAction`, `IModItemActionContext`,
   `IModEffectItem`, `IModItemDefinition.UsableOnLimb` (+ the data class), `IModContext.ItemActions`,
   `IModEffectBody.NativeObject`, and the three census members.
2. **Runtime**: `ModItemEffectStore` (the framework-wide leaf, the same shape as `ModLiquidEffectStore`)
   and `ModItemActionsAdapter` (the per-mod permission / ownership / reachability / duplicate rails), plus
   the wiring through `ModContext`, `ModLifecycle` (including the failed-load withdrawal) and
   `ModComposition`.
3. **Game Adapter**: `ItemActionInvoker` (what the two installed delegates call: resolve per call, build
   the context, invoke inside a guard), `ModItemActionContext`, `ModEffectItem`, the two new mapping arms,
   and the build site installing both delegates instead of the reporting no-op alone.
4. **Tests**: the registration rails, the two delegates' assembly, and the per-call resolution.
5. **Docs**: the `mod-api.md` pair, the glossary pair, the terminology registry, the alignment record, a
   decision entry, the content-mod example check, and this ticket.

## What landed

| Piece | Where | What it is |
|---|---|---|
| The registration face | `src/CasualtiesUnknownOnline.Abstractions/IModItemActions.cs`, `ModItemAction.cs`, `IModItemActionContext.cs` | the two halves, the callback and what it landed on (item, body, limb, owner, hatch) |
| The item write surface | `src/CasualtiesUnknownOnline.Abstractions/IModEffectItem.cs` | `Condition` / `AddCondition` / `SetCondition` / `NativeObject` |
| The declaration the limb half needed | `IModItemDefinition.UsableOnLimb` + `ModItemDefinition.UsableOnLimb` | the flag `PlayerCamera.ApplyWoundItem` gates the item's limb action on — absent before this cycle, so a mod item could not be offered on a limb at all |
| The census members | `ModBodyVital.Hemothorax`, `ModLimbVital.DislocationTimer` / `BoneHealTimer` | appended to the enums so no existing member's value moves; each documented with the game's own field |
| The shared hatch | `IModEffectBody.NativeObject` | the body's object, so all three accessors expose the object they wrap |
| The registry | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModItemEffectStore.cs`, `ModItemEffectRegistration.cs` | the framework-wide leaf, one table per half, a live lookup per call |
| The rails | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModItemActionsAdapter.cs`, and `ModLiquidEffectsAdapter.cs` for the liquid half's matching limb-side rail | permission, valid id, ownership (kind-aware), reachability (the item's both halves, the liquid's limb half), isolation, duplicate |
| The definition read the rail needed | `ModContentStore.Find` | the ownership rail became `Find(...) is not IModItemDefinition`, so one query answers both "is it yours" and "what does it declare"; `IsRegisteredAs` now derives from it |
| The invoker | `src/CasualtiesUnknownOnline.GameAdapter/ModEffects/ItemActionInvoker.cs` | what the two installed delegates call; resolves per call, guards the invocation, reports the unregistered half |
| The context and accessor | `src/CasualtiesUnknownOnline.GameAdapter/ModEffects/ModItemActionContext.cs`, `ModEffectItem.cs` | built over the live `Item` / `Body` / `Limb` |
| The build site | `ModItemInfoFactory.Build` + `GameAdapterItemContentProvider` | installs both delegates from the invoker, maps `UsableOnLimb`, and reports a declared flag with no registered action at injection time (the frame late enough to see the registration) |
| The pins | `tests/…/Mods/ModItemActionRegistrationTests.cs` (15 cases), `tests/…/Mods/ModLiquidEffectRegistrationTests.cs` (2 new cases for the liquid rail), `tests/…/Patching/DeclaredBehaviourContentTests.cs` (7 new cases, 10 item cases in all, over the real build site) | the rails as behaviour, the assembly and the per-call resolution through the real adapter and the real build site |

## Self-check table (as delivered)

| Mechanism | Change | Evidence |
|---|---|---|
| Registration rails | permission, valid id, ownership, kind, reachability both halves, isolation, duplicate, withdrawal, and the liquid half's matching limb-side rail | `ModItemActionRegistrationTests` — 15 cases — and `ModLiquidEffectRegistrationTests`, which gained two for the liquid rail; every refusal asserts the log line that names the reason |
| Review round | one major and seven minors from the independent adversarial review, all fixed in the same commit | the use-half rail now requires `Usable` (a `UsableWithLmb`-only registration is refused and cased); the liquid rail above; three refusals given the log assertions the checklist claimed; a case that registers AFTER the build, which is the only shape that pins per-call resolution; the census's twelve unnamed body fields named; `GetComponent` / `transform` recounted over one consistent surface (103 / 111); the `LiquidItemInfo` invariant and the applied-item consequence written; the vanilla-id-collision registration recorded as a limit; four stale-comment/stale-number nits |
| The definition read | one query answers ownership and declaration | `ModContentStore.Find`; `IsRegisteredAs` delegates to it, so the two cannot drift |
| Delegate assembly | a declared flag always carries a delegate | `DeclaredBehaviourContentTests` — `usableOnLimb` mapped, `useLimbAction` present for a declared limb usability, absent without it, and no invented use action without `Usable` / `UsableWithLmb` |
| Per-call resolution | the installed delegate resolves the registered action at call time | the same suite: the registered case logs the owning mod, the unregistered case logs "no mod registered", and the use-only registration leaves the limb half on the reporting path. L0 cannot build a live `Item` / `Body`, so the invocation takes the invoker's missing-object branch — which is exactly why the two paths are told apart by the line and not by the effect |
| The named operations | enum ↔ arm, read field == write field | `ModEffectOperationGateTests` (unchanged, still green at 33 / 9 members) |
| The public surface | the contract addition is the reviewed baseline | `ApiSurfaceGateTests` — the emitted candidate was reviewed and adopted as a PURE ADDITION (`git diff --numstat` reads `24  0`: no removal, no tombstone, no rewritten line) |
| The report at load | a declared flag with no registered action is named | `GameAdapterItemContentProvider.ReportUnregisteredActions`, asked per half, at injection time; source-verified — the call site needs `Item.GlobalItems`, so it rides the acceptance row |

## Limits (as delivered)

- **The end-to-end half needs a live session.** L0 builds no live `Item` or `Body`, so neither the guarded
  invocation nor the operation implementations are driven at the level a real use reaches: the evidence is
  the resolution cases above plus the source-level gate, and the acceptance row is what judges the rest.
  The mod that registers an action — the fixture half that was missing — now exists: `example.dressing`
  declares both `Usable` and `UsableOnLimb`, and `ExampleMod` registers both halves in `Bind`
  (`src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs`, `RegisterAuthoredBehaviour`), so the use half
  is what the game's own `Body.UseItem` runs and the limb half is what the wound view runs — and, on a
  two-client session, what CUO's cross-player limb-tool chain runs on the treated player's own client.
  The declared-flag-with-no-function shape sits beside it as `example.placebo` (usable, nothing
  registered), which is the sibling ticket's item row.
- **No red was observed for the guard.** A throwing action costs the action and not the gesture, but the
  throw can only happen on the path L0 cannot reach, so the guard is source-verified here and rides the
  acceptance row.
- **A mod-authored use action is not claimed by the cross-player solid-food chain.** That chain decides
  membership by reading the item's own delegate BODY for a call to `Body.Eat` / `Body.Drink`
  (`docs/backlog/done/mod-cross-player-solid-food-semantics.md` step 3), and an installed invoker delegate
  has no such call in it. A mod item that feeds a body is therefore used locally; making it feedable across
  players needs its own decision about how a registration declares that intent.
- **The named-operation surface is not the whole census.** The rule stated above leaves movement,
  environment, interaction modes, component lookups, object replacement, world writes and item
  destruction to the engine hatch, where a mod leaves the API's promise and declares what it binds.
  Enumerated BY NAME — the twelve body fields listed under the rule, the item's own engine work, and the
  body actions the census lists with no named operation (`DropItem`, `PickUpItem`, `AutoPickUpItem`,
  `RemoveEye`, `TakeANap`, `DoGoreSound`, `SlotOf`, `talker`) — rather than left to the reader's inference.
- **A registration for a definition whose id the vanilla item table already owns is silently dead.** The
  provider accepts such a definition but never injects it (the vanilla entry, and its `cu:<id>` resource
  id, stays authoritative), so both rails pass and the registration logs success while the game keeps
  running the vanilla `ItemInfo` and its vanilla delegate. The provider's own
  "already exists in the vanilla item table — the mod definition is not injected" warning is the only
  trace, and it is emitted at injection time; the item-action registration has no earlier way to know.
- **A mod may not author a vanilla item's action**: that is a content override, still parked in
  `mod-content-ceiling.md` Part 3.C.
- **The register-then-author order is load-bearing for the load-time report.** A mod registers its content
  first and its action second (the rail requires that order), and the report runs at injection; a mod that
  registers its action from `Update` after the item table was injected keeps the reporting path silently.
  The registration itself is unaffected — the invoker resolves per call — so the cost is a missing line,
  not a missing action.

## Non-goals

- Not a second write surface: the body and limb accessors are the ones the liquid half established, and the
  item accessor carries only what the item census writes.
- Not a new wire family or a host-side change: the cross-player limb-tool chain already runs the item's own
  delegate on the affected side.
- Not the wearable set (landed earlier as
  `docs/backlog/review/mod-item-wearable-declaration.md`) nor the remaining Part 3.A read-only fields
  (`rec`, `onlyHoldInHands`, `combineable`, `ignoreDepression`, `scaleWeightWithCondition`,
  `jumpHeightMultChange`, `slotRotation`) — each keeps its own consumer rule.
