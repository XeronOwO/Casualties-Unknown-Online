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
2. **The kind comes from the kind family, never from the attribute or the class name.** A class derives from
   exactly one per-kind base (`ModItemContent`, `ModRecipeContent`, `ModLiquidContent`,
   `ModLiquidTileContent`, `ModTileContent`, `ModBuildingContent`, `ModStructureContent`,
   `ModStatusContent`, `ModMoodleContent`), which supplies `Kind` (fixed by the type, as in decision 247),
   the `SchemaVersion` default, and the registration-time validation seam; `Id` is the declaration's own
   override. A class reaching two kinds is refused at scan with a log naming it — never guessed.
   Why a base and not a bare interface: **.NET Framework 4.8 has no default interface members**, so a
   per-kind base is what keeps the identity boilerplate in one place instead of an adapter per kind.
3. **Capability facets are additive interfaces on the same class** (`IModUsableContent`,
   `IModWearableContent`, `IModContainerContent`, `IModBatteryContent`, `IModLightContent`,
   `IModToolContent`, `IModGunContent`, `IModVisualContent`): they are columns of ONE entry, never a second
   entry and never a second id. "An item that is also usable" is one item registration with a facet — not
   two registrations.
4. **One class = one registration.** The registry, the catalog, the ownership query, the console vocabulary
   and the binders are unchanged underneath: the scan is a declaration front-end over
   `IModContent.TryRegister(IModContentDefinition)`, not a second registry.
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

- Whether a facet interface replaces the DTO's nested member objects (`Tool`, `Gun`, `Container`,
  `Battery`, `Light`, `Visual`) or maps onto them 1:1; either way the provider reads one shape.
- Whether the scanned instance is stored as it is (decision 247's rule) or snapshotted at registration once
  its members have been read.

## Non-goals

- No second registry, no second permission rail, no second binder.
- No change to the wire or the save: content is process-local either way.
- No auto-loading of assemblies that declare no mod.
- No compatibility shim: the code path and the scan are the same contract, so neither is kept alive for the
  other.

## Acceptance (mod-author visible, judged at the acceptance batch)

- A mod declares an item and a recipe by attribute only, with no registration call, and both materialize in
  game tables.
- A class that reaches two kinds is refused with a log naming it; the other declarations still bind.
- A declaration whose getter throws is refused, named, and does not stop its siblings.
- Two mods declaring the same bare id still report the existing conflict; the scan adds no new id rule.
- An assembly without `[CuoMod]` that declares content by attribute alone binds nothing (and says so), while
  the same definitions registered through code bind.
