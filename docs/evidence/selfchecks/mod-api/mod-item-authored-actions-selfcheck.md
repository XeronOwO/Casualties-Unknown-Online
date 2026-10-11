# Mod-authored item actions — self-check (2026-10-11)

Ticket: `docs/backlog/review/mod-item-authored-actions.md`, cut out of the ceiling ticket's Part 3.A on
2026-10-11 and landed the same day. The design record it follows is the liquid half's,
`docs/backlog/review/mod-authored-effects.md` (decision 255 / 256): a function is not data, so it is
registered in code keyed by the content id its owner registered. **Scope of this cycle: the ITEM half** —
`ItemInfo.useAction` and `ItemInfo.useLimbAction` become authorable, the flag the limb half needs
(`usableOnLimb`) becomes declarable, and the reachability rail is added — on the item's own two halves,
and on the LIQUID half's limb side once the review put the same question to it.

Citations: `reversing/` paths carry line numbers (that tree is never edited); our own `src/`, `tests/` and
`docs/` are cited by path plus quoted text.

## 1. What landed

| Artifact | Change |
|---|---|
| `IModItemActions`, `ModItemAction`, `IModItemActionContext` (new) | The registration face reached through the new `IModContext.ItemActions`: `TryRegisterUse` where `ItemInfo.useAction` stands, `TryRegisterLimbUse` where `ItemInfo.useLimbAction` stands, one `ModItemAction` delegate for both halves. `Experimental`. |
| `IModEffectItem` (new) | The item's named operations: `Condition`, `AddCondition` (the plain field arithmetic the game's own items perform) and `SetCondition` (through the game's own `Item.SetCondition`), plus `NativeObject`. `Advanced`. |
| `IModItemDefinition.UsableOnLimb` + `ModItemDefinition.UsableOnLimb` | The declaration the limb half was missing entirely: the flag `PlayerCamera.ApplyWoundItem` gates the item's limb action on, and the one CUO's cross-player limb-tool chain reads. |
| `IModEffectBody.NativeObject` | The body's engine object, so all three accessors expose the object they wrap (the limb's and the item's already did). |
| `ModBodyVital.Hemothorax`, `ModLimbVital.DislocationTimer` / `BoneHealTimer` | The three members the item census adds, APPENDED so no existing member's value moves. |
| `ModItemEffectStore` + `ModItemEffectRegistration` (new) | The framework-wide leaf, the same shape as `ModLiquidEffectStore`: one table per half, a live lookup per call. |
| `ModItemActionsAdapter` (new) | The per-mod rails: permission, null, valid id, ownership (kind-aware), reachability, isolation, duplicate. |
| `ModLiquidEffectsAdapter` | The liquid half's MATCHING limb-side rail, added in the same cycle: the review asked whether the item rail's question also fails on the liquid side, and it does — `onHealthUse` sits behind `healthUsable` / `injectable`, so a liquid declaring neither could not reach a limb call site and its registered effect was dead code with a success line. |
| `ModContentStore.Find` | The definition read the reachability rail needs, so one query answers "is it yours" and "what does it declare"; `IsRegisteredAs` now derives from it. |
| `ItemActionInvoker`, `ModItemActionContext`, `ModEffectItem` (new) | The Game Adapter bridge: resolve per call, build the context over the live `Item` / `Body` / `Limb`, invoke inside a guard, report the unregistered half. |
| `ModItemInfoFactory` + `GameAdapterItemContentProvider` | The build site installs both delegates from the invoker instead of the reporting no-op alone, maps `UsableOnLimb`, and reports a declared flag with no registered action at injection time. |
| `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` | A new "Mod-authored item actions" section, the permission row, and the two member counts it moved (33 body vitals, nine limb vitals). |
| `docs/standard/terminology.txt` + both glossaries | `mod-authored item action` registered before its first human-facing use; the glossary pair gains "mod-authored action". |
| `docs/contracts/abstractions-api-baseline.txt` | The reviewed addition: 24 added lines, no removed and no rewritten line, byte-identical to the gate's emitted candidate. |
| `docs/decisions/active.md` | Decision 260. |
| `tests/…/Mods/ModItemActionRegistrationTests.cs` (new) | 13 cases over one store and one content store. |
| `tests/…/Patching/DeclaredBehaviourContentTests.cs` | 6 new cases over the real build site (9 item cases in all). |
| `tests/…/NormativeGates.Tests/ModEffectOperationGateTests.cs` | Its doc names both censuses and loses a stale `todo/` path. |
| `tests/…/Fakes/GameAdapterProviderFactory.cs` | The item action store joins the by-type seam list, which is the factory's whole point: a new provider dependency is a deliberate line rather than a `MissingMethodException` in every suite at once. |

## 2. The decisions this cycle had to make

**2.1 The reachability rail is a refusal, not a load-time report.** Both native calls sit behind a flag the
definition carries — `Body.cs:2479` runs `item.Stats.useAction(this, item)` behind `if (item.Stats.usable)`,
`PlayerCamera.cs:739-762` runs `useLimbAction` behind `ActuallyUsableOnLimb`, which for a non-container is
`usableOnLimb` itself. So an action registered for an item that declares neither flag is code no gesture can
ever run. The liquid half's own counterparts are reports (`HasLimbUse` asked at injection), because there the
declaration is the thing that could be unmatched; here the registration itself is the mismatch, and the
registration call is where the author can still act on it. The refusal messages name the flag, not the
outcome.

**2.2 One item, one use action.** A definition carrying a `Tool` or `Gun` behaviour already HAS a use
action — `ModItemInfoFactory` builds one for each. Accepting a registered action beside it would make which
function runs depend on registration order, which is the shape the workflow's entry-isolation rule forbids:
the item's own data says which action each entry runs. So the use half is refused for those definitions,
with the reason named.

**2.3 The write surface is shared, not duplicated.** `IModEffectBody` / `IModEffectLimb` were NOT copied into
an item-specific pair. The game hands `LiquidType.onDrink` / `onHealthUse` and `ItemInfo.useAction` /
`useLimbAction` the same body and the same limb, so a second pair would be two names for one thing and would
drift. What the item side needed beyond them is the item itself — one accessor, three operations.

**2.4 The named-operation rule, stated before it was applied.** The liquid census covered "every write the
game's own shipped liquids perform"; the item census is broader and includes kinematics (`moveForce`,
`maxSpeed`, `jumpSpeed`), environment (`wetness`, `snowAmount`), interaction modes (`sleeping`,
`usingSleepingBag`) and the whole engine/world tail (`Sound.Play` 83, `Resources.Load` 51, `FluidManager` 21,
`WorldGeneration` 11). Carried literally it would make the contract a mirror of the game's internals. The
rule applied instead, and written into the ticket: a member joins the surface when it is a STATE QUANTITY of
a body, a limb or the item, written as an accumulate-or-pin by the game's own content; everything else is
engine work and belongs to the hatch. By that rule the census adds exactly three members.

**2.5 The hatch belongs to the accessor that wraps the object.** `IModEffectLimb.NativeObject` already
existed and `IModLiquidEffectContext.NativeObject` named the body's; the item half needed both the item's
object and a consistent way to the body's. So `IModEffectBody.NativeObject` was added and
`IModItemActionContext.NativeObject` carries the item's, keeping the rule "an accessor exposes the object it
wraps, a context exposes the subject the action landed on" true for both families.

## 3. The census, and what it left to the hatch

Measured 2026-10-11 in `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`: **195** delegate assignments —
**139** `useAction`, **56** `useLimbAction`, **16** `ItemInfo` instances carrying both — and a name-by-name
sweep of the 264 decompiled files finds the two delegates assigned nowhere else.

| Group | Outcome |
|---|---|
| `item.condition` (78 use, 22 limb) | named operations (`IModEffectItem`) |
| `item.decayMultiplier` (1) | the hatch: the game itself uses it as a per-item decay switch, which is a mechanism, not an effect |
| body vitals the census writes | `ModBodyVital` already carries all but thirteen of them. `hemothorax` was ADDED; the other twelve keep no named operation and are hatch residents by 2.4's rule, named here one by one so "the tail is enumerated" is true — `attackCooldown` (`= 0.5f` ×3, `= 15f`), `hearingLoss` (`-= 8f`), `eatTime`, `temporarySlowdown`, `usedNeuralBooster` (five per-item MECHANISM writes), and `wetness`, `snowAmount`, `sleeping`, `usingSleepingBag`, `moveForce`, `jumpSpeed`, `maxSpeed` (environment and interaction modes, and the kinematic parameters) |
| limb vitals the limb actions write | already in `ModLimbVital`, except the two recovery timers — added |
| body actions (`Eat` 45, `Drink` 12, `Attack` 27, `Ragdoll` 3, `vomiter` 4) | `Eat` / `Drink` / `Ragdoll` / `Vomit` are named operations; `Attack` is what the `Tool` behaviour is for |
| body actions with no named operation (`DropItem` 5, `PickUpItem` 7, `AutoPickUpItem` 3, `RemoveEye` 2, `TakeANap` 1, `DoGoreSound` 1, `SlotOf` 4, `talker` 37) | the hatch: per-item mechanisms and presentation |
| engine / world (`GetComponent` 103, `transform` 111, `Sound.Play` 83, `Resources.Load` 51, `Instantiate` 5, `Object.Destroy` 5, `WorldGeneration` 11, `FluidManager` 21, `AddComponent` 15, `CreateTemporarySprite` 13 — every figure over the same 195 delegate bodies, with no per-prefix narrowing) | the hatch, by 2.4 |

## 4. Mechanism inventory

| # | Mechanism | Consumer that reads it | State |
|---|---|---|---|
| 1 | `IModItemActions` / `ModItemAction` / `IModItemActionContext` | a mod's own `Bind`/`Initialize` registers; `ModItemActionsAdapter` decides; `ItemActionInvoker` invokes | new, `Experimental` |
| 2 | `ModItemEffectStore` | `ItemActionInvoker` (two per-call lookups) and the load-time report (`HasUse` / `HasLimbUse`) | new |
| 3 | `ModContentStore.Find` | `ModItemActionsAdapter`'s reachability and ownership rails | new read over an existing table |
| 4 | `IModItemDefinition.UsableOnLimb` | `ModItemInfoFactory` (installs `usableOnLimb` and the delegate), `GameLimbUseFacts.IsLimbActionItem` (the cross-player limb-tool chain), `ItemInfo.ActuallyUsableOnLimb` (the game's own wound view) | new declaration, three readers |
| 5 | `IModEffectItem` | the mod's registered action, and the id-addressed `IModGameState` read is a separate surface | new, `Advanced` |
| 6 | `IModEffectBody.NativeObject` | a mod action or effect that wants the game's own object | new member on an existing accessor |
| 7 | `ModBodyVital.Hemothorax`, `ModLimbVital.DislocationTimer` / `BoneHealTimer` | `ModEffectBody` / `ModEffectLimb` mapping arms, pinned by `ModEffectOperationGateTests` | new enum members |
| 8 | `ItemActionInvoker` | the two delegates `ModItemInfoFactory` installs | new |

Nothing on this list lacks a reader: each row names the call site that consumes it, and no new wire, kernel
or host mechanism was introduced (the registrations are local, and the cross-player limb-tool chain needed no
change to carry a mod item).

## 5. What this cycle does NOT prove

- **The end-to-end half.** L0 builds no live `Item` or `Body`, so neither the guarded invocation nor the
  operation implementations run at the level a real use reaches. The evidence here is the resolution cases
  plus the source-level gate; the acceptance row judges the rest, and it needs a mod that registers an
  action — the sample mod's dressing declares neither `Usable` nor `UsableOnLimb`.
- **No red for the guard.** A throwing action costs the action and not the gesture, but the throw can only
  happen on the path L0 cannot reach, so the guard is source-verified.
- **The load-time report's own call site.** `ReportUnregisteredActions` runs from the injection frame, which
  reads `Item.GlobalItems`; a case would need the game's table.
- **A mod-authored use action is not claimed by the cross-player solid-food chain.** That chain decides
  membership by reading the item's own delegate BODY for a call to `Body.Eat` / `Body.Drink`
  (`docs/backlog/done/mod-cross-player-solid-food-semantics.md`, step 3), and an installed invoker delegate
  contains no such call. A mod item that feeds a body is used locally. Named in the ticket's Limits rather
  than papered over with a weaker membership test.
- **The `decayMultiplier`/kinematics tail of the census is reachable only through the hatch**, where a mod
  leaves the API's promise and declares what it binds. That is a scope decision (2.4), not an oversight.

## 6. Verification as run

| Command | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings, 0 errors |
| `dotnet test … --filter "…ModItemActionRegistration\|…ModLiquidEffect\|…DeclaredBehaviour"` | 38 passed after the review round (the one initial failure was a case whose fixture declared both flags; the fixture was made per-case, and a second round of failures came from the four suites that still constructed the provider by hand — all moved onto the shared factory) |
| `dotnet test … --filter "FullyQualifiedName~Gate"` | 615 + 114 gates; the only failure was `ApiSurfaceGateTests`, whose emitted candidate was reviewed and adopted (`24  0`) |
| `dotnet format CasualtiesUnknownOnline.slnx` | run once after the last C# edit |
| `dotnet test CasualtiesUnknownOnline.slnx` (full) | 615 gates + 5051 behaviour, green — 23 cases above the 5028 the cycle started from |
| the independent adversarial review (FULL tier, fresh context, frozen tree, read-only) | 1 major / 7 minor / 3 nit, no blocker; every finding fixed in the same commit, and its report is `.agent-local/reviews/mod-item-authored-actions-review.md` |

The mutation that pins the reachability rail is the rail itself: replacing `IsReachable`'s body with
`return true;` turns **four** cases red — the two flag refusals, the Tool/Gun refusal, and the positive limb
case, whose second assertion (`TryRegisterUse` is refused for a limb-only definition) catches the disabled
rail from the accepting side. Restoring the body turns all 13 green again.
