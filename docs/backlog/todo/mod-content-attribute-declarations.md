# Content declarations by attribute: a scanned declaration instead of a Bind list

- Status: Todo — **raised 2026-10-08 by the user**: "对于定义这玩意，能不能直接做成 Attribute + 反射？
  反射期间看实现了哪些接口决定这个东西有什么性质" — declarations live next to the code that owns them,
  the framework discovers them, and the interfaces a class implements decide what it is. The design below
  was settled in the same conversation (the multi-interface answer is the user's question and this ticket's
  answer to it).
- Priority: High
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-typed-registration.md` (the typed contract this sits on),
  `docs/backlog/todo/mod-api-no-opaque-envelopes.md` and `docs/backlog/todo/mod-api-typed-seams.md` (both
  change the shape this scanner reads, so they land first),
  `docs/backlog/todo/mod-authored-effects.md` (its code-registration surface is the same question),
  `docs/en/reference/mod-api.md` (the page a mod author reads), decision 247.
- Source: the user's 2026-10-08 question and the follow-up ("它实现多个接口…是一个注册入口还是每个接口实现都要注册一遍？").

## The shape (settled)

1. **A bare marker attribute declares content**: `[ModContent]` on a class. It carries NO data — not even
   the id — because the address belongs to the definition (decision 247) and "Attribute 只负责发现" is the
   user's rule. Discovery instantiates the class and reads its members.
2. **The kind comes from the kind interface the class implements, never from the attribute or the class
   name.** Nine kind interfaces (`IModItemContent`, `IModRecipeContent`, `IModLiquidContent`,
   `IModLiquidTileContent`, `IModTileContent`, `IModBuildingContent`, `IModStructureContent`,
   `IModStatusContent`, `IModMoodleContent`), each carrying its kind's data members and extending
   `IModContentDefinition`. **Any type that implements one is registrable**: a mod's own class, or the
   framework's DTO — `ModItemDefinition` becomes one implementation of `IModItemContent` rather than the only
   accepted shape, which is what keeps materialization open instead of binding it to a framework type (the
   user's requirement: "任何自定义类型只需要实现那个接口"). Implementing two kind interfaces is refused at
   scan with a log naming the class — never guessed — and a `Kind` that disagrees with the implemented kind
   interfaces is the same named refusal.
   A per-kind base (`ModItemContent`, …) exists as an OPTIONAL convenience that supplies the `Id` / `Kind` /
   `SchemaVersion` boilerplate; it is never required, because **.NET Framework 4.8 has no default interface
   members** and a mod class that already has a base (a `MonoBehaviour`, its own hierarchy) must still be
   able to declare content.
3. **Capability facets are additive interfaces on the same class** (`IModUsableContent`,
   `IModWearableContent`, `IModContainerContent`, `IModBatteryContent`, `IModLightContent`,
   `IModToolContent`, `IModGunContent`, `IModVisualContent`): they are columns of ONE entry, never a second
   entry and never a second id. "An item that is also usable" is one item registration with a facet — not
   two registrations.
4. **One class = one registration.** The registry, the catalog, the ownership query, the console vocabulary
   and the binders keep their shape: the scan is a declaration front-end over
   `IModContent.TryRegister(IModContentDefinition)`, not a second registry. What does change is what the nine
   providers read — the kind interface instead of the concrete DTO — and that change alone is what lets a
   mod-authored type materialize.
5. **Behaviour seams stay per-interface**: `IModCommand`, the declared-packet chain,
   `IResourceLocationMatchStage`, the entity-spawn and placement seams already work as "implement the
   interface, get the capability". A class may declare content and behaviours at once — different
   registries, no ambiguity.
6. **The code path stays, and it is not a fallback**: an assembly without a `[CuoMod]` declaration must
   register through `context.Content.TryRegister` (the in-tree precedent is the pinyin mod, split into
   `PinyinSearch` and `PinyinSearch.Core`), and generated or bulk content (twenty recipe variants from one
   table) is code's job. Both routes feed one registry, one permission rail, one binder.

## Why not one entry per implemented interface

The game has ONE row per addressable thing (`Item.GlobalItems` is keyed by id, and usability is a column of
that row), so a second entry would need a second address and a cross-reference between them. The tree's own
answer to a hybrid is a distinct kind that references another entry — `liquidtile` is its own kind with
`LiquidId` / `FillLiquidId`, not "liquid and tile registered twice". Stated as a rule: **as many entries as
things the game can address and delete on their own.**

## Discovery and determinism rules

- Only assemblies that declare a mod are scanned; **a second `[CuoMod]` in one assembly is refused with a
  log and does not take effect** (one assembly, one mod), which is also what makes a candidate's namespace
  and ids unambiguous.
- Ids must be unique per mod at registration (the existing rail); every id-ordered consumer then sorts by
  the canonical id — tile index allocation, the world-fluid byte and the worldgen streams already depend on
  a stable order.
- A declaration whose member getter throws is refused with a log naming it, and neither the scan nor the
  other declarations are taken down: the existing "one broken mod never blocks the others" policy, one step
  finer. Executing the declaration's code at discovery is expected, not a hazard: mod classes are already
  framework-instantiated (`ModRegistry` requires a public parameterless constructor).
- Null collections keep meaning "none" at the read seam (decision 244's member half).

## Open at implementation

- How a facet interface meets the DTO's nested member objects (`Tool`, `Gun`, `Container`, `Battery`,
  `Light`, `Visual`): flatten them into flat facet members (the DTO delegates to its nested objects) or let
  the facet interface hand the nested object back. Either way the provider reads ONE shape, and either way a
  mod-authored class is never forced to `new` a framework type to fill a facet.
- Whether the scanned instance is stored as it is (decision 247's rule) or snapshotted at registration once
  its members have been read.

## Non-goals

- No second registry, no second permission rail, no second binder.
- No framework type required to declare content: the nine DTOs are implementations of the kind interfaces,
  not the contract, and the code path keeps accepting either.
- No change to the wire or the save: content is process-local either way.
- No auto-loading of assemblies that declare no mod.
- No compatibility shim: the code path and the scan are the same contract, so neither is kept alive for the
  other.

## Acceptance (mod-author visible, judged at the acceptance batch)

- A mod declares an item and a recipe by attribute only, from ITS OWN classes implementing the kind
  interfaces (no framework DTO instantiated anywhere), with no registration call, and both materialize in
  game tables.
- The same two definitions registered through code, as `ModItemDefinition` instances, still materialize —
  one provider, two ways to feed it.
- A class that reaches two kinds is refused with a log naming it; the other declarations still bind.
- A declaration whose getter throws is refused, named, and does not stop its siblings.
- Two mods declaring the same bare id still report the existing conflict; the scan adds no new id rule.
- An assembly without `[CuoMod]` that declares content by attribute alone binds nothing (and says so), while
  the same definitions registered through code bind.
