# Content declarations by attribute: a scanned declaration instead of a Bind list

- Status: Review — **stage B landed 2026-10-09** (see *Stage B (landed)*): nothing is left to develop here,
  and the acceptance batch is pending. It was **raised 2026-10-08 by the user**: "对于定义这玩意，能不能直接做成
  Attribute + 反射？反射期间看实现了哪些接口决定这个东西有什么性质" — declarations live next to the code that
  owns them, the framework discovers them, and the interfaces a class implements decide what it is. The design
  below was settled in the same conversation (the multi-interface answer is the user's question and this
  ticket's answer to it).
- Priority: High
- Category: Mod platform / mod API
- Related: `docs/backlog/review/mod-content-typed-registration.md` (the typed contract this sits on),
  `docs/backlog/review/mod-api-no-opaque-envelopes.md` and `docs/backlog/review/mod-api-typed-seams.md` (both
  change the shape this scanner reads, so they land first),
  `docs/backlog/todo/mod-authored-effects.md` (its code-registration surface is the same question),
  `docs/backlog/review/mod-content-nested-member-contracts.md` and
  `docs/backlog/todo/mod-content-fingerprint.md` (the two halves this ticket handed on rather than dropped),
  `docs/en/reference/mod-api.md` (the page a mod author reads), decisions 247 and 252.
- Source: the user's 2026-10-08 question and the follow-up ("它实现多个接口…是一个注册入口还是每个接口实现都要注册一遍？").
- Stage A (landed 2026-10-09): the nine kind interfaces and their ready-made implementations are in,
  every consumer reads the interface rather than the framework's class, and the derived rules a
  declaration feeds moved to the framework — evidence
  `docs/evidence/selfchecks/mod-api/mod-content-declaration-contract-selfcheck.md`, decision 251.
- Stage B (landed 2026-10-09): the bare `[ModContent]` attribute, the scan that discovers it and registers
  what it owns through the code path's own `IModContent.TryRegister`, the named refusals (two kind
  contracts, none, a `Kind` that disagrees with the contract, a mod class declaring content on itself, a
  class the scan cannot instantiate, and a member getter that throws), `ModContentContract` as the one
  home of the interface-to-kind mapping, and the settle of the two items the ticket left open — evidence
  `docs/evidence/selfchecks/mod-api/mod-content-declaration-binding-selfcheck.md`, decision 252. The
  example mod declares its own content this way, so the production path has a real consumer.

## Superseded and handed on (2026-10-09)

Three sentences of this ticket do not survive contact with the tree, and one acceptance row belongs to
work this ticket does not carry. Each is recorded here rather than quietly dropped or quietly narrowed.

1. **"A second `[CuoMod]` in one assembly is refused and does not take effect" is SUPERSEDED.** It would
   refuse this repository's own example assembly (which declares `ExampleMod` and `ExampleMachineMod`)
   and the whole test-mod architecture (one test assembly, dozens of fixture mods that must load
   together), and its stated purpose — "so a candidate's namespace and ids are unambiguous" — is about
   the DECLARATIONS, not the mods. The scan therefore answers the ambiguity one step later: ownership is
   NESTING (a declaration inside a mod class is that mod's), a declaration that is not nested in a mod
   belongs to the mod its assembly declares — which therefore has to be the only one — and a top-level
   declaration in a multi-mod assembly is refused with a log naming it while the mods themselves load.
   The count is read from the `[CuoMod]` declarations themselves, so a mod class that fails discovery
   still makes its assembly ambiguous, which is the fail-closed direction.
2. **The optional per-kind base classes (`ModItemContent`, …) are NOT built.** They were specified to
   supply the `Id`/`Kind`/`SchemaVersion` boilerplate, and C# does not allow that shape: an abstract
   class that leaves interface members unimplemented fails with CS0535 (measured on the first attempt in
   this cycle — 148 errors, one per unimplemented member), so such a base would have to restate the
   kind's other ~20 members to save three. The only shape that would earn its place is a base whose
   members are `virtual` and default to the data class's values, and that creates a SECOND home for
   defaults the data classes own — the drift the contract's one-home rule exists to prevent. A mod class
   that already has a base was never the reason for them: implementing the interface works there today,
   which is the whole point of stage A.
3. **The content fingerprint moves OUT of this ticket**, with its acceptance row, to
   `docs/backlog/todo/mod-content-fingerprint.md`. It is a save- and handshake-side consequence of
   computed definitions rather than a requirement of the scanner.
4. **The nested member contracts move OUT of this ticket**, with the acceptance row that names them, to
   `docs/backlog/review/mod-content-nested-member-contracts.md`: `IModItemTool` and its thirteen siblings
   do not exist, and the collection members are typed `List<ModT>` today, so allowing a mod-authored
   nested implementation is a DTO type change with its own consumer sweep.

## The shape (settled)

1. **A bare marker attribute declares content**: `[ModContent]` on a class. It carries NO data — not even
   the id — because the address belongs to the definition (decision 247) and "Attribute 只负责发现" is the
   user's rule. Discovery instantiates the class and reads its members.
2. **Every declaration type is an interface plus a ready-made implementation.** The nine kind interfaces are
   `IModItemDefinition`, `IModRecipeDefinition`, `IModLiquidDefinition`, `IModLiquidTileDefinition`,
   `IModTileDefinition`, `IModBuildingDefinition`, `IModStructureDefinition`, `IModStatusDefinition`,
   `IModMoodleDefinition`, each extending `IModContentDefinition`; today's classes (`ModItemDefinition`, …)
   become the framework's implementations of them rather than the only accepted shape. The same rule applies
   one level down to every member type a provider reads — `IModItemTool`, `IModItemGun`, `IModItemContainer`,
   `IModItemBattery`, `IModItemLight`, `IModItemVisual`, `IModItemSpriteAnimation`,
   `IModItemLimbWornSprite`, `IModRecipeIngredient`, `IModCraftingQuality`, `IModBuildingDrop`,
   `IModTileDrop`, `IModMoodleAnimation`, `IModLimbMoodleBinding` — because a mod that computes one value
   will want to compute the nested ones. So there are two rungs: `new ModItemDefinition { … }` when the
   fields are constants, and implementing `IModItemDefinition` when a value is computed ("提供平台，也给出
   上限，但不堵死上限"). The default implementations stay `sealed` plain data carriers; partial customisation
   is COMPOSITION — implement the interface and hand back a filled default for the members you do not touch —
   never inheritance from a data class.
3. **One class-level contract: the kind interface.** The kind comes from which kind interface the class
   implements, never from the attribute or the class name. A class implementing two kind interfaces is
   refused at scan with a log naming it — never guessed — and a `Kind` that disagrees with the implemented
   interface is the same named refusal. A per-kind base class (`ModItemContent`, …) exists as an OPTIONAL
   convenience that supplies the `Id` / `Kind` / `SchemaVersion` boilerplate; it is never required, because
   **.NET Framework 4.8 has no default interface members** and a mod class that already has a base (a
   `MonoBehaviour`, its own hierarchy) must still be able to declare content.
   Capabilities are NOT extra interfaces on the class: "an item that is also usable, wearable or a container"
   is the same single registration, whose `Tool` / `Gun` / `Container` / `Battery` / `Light` / `Visual`
   members are themselves interface-typed. That keeps the existing data shape (nothing is flattened onto the
   class), keeps one entry and one id per declaration, and is a smaller concept count than a
   facet-interface family.
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

- Only assemblies that REFERENCE the mod surface are scanned, and the census of an assembly runs once.
  **Ownership is nesting**: a declaration inside a mod class is that mod's; a declaration that is not
  nested in a mod belongs to the mod its assembly declares, which therefore declares exactly one; a
  top-level declaration in an assembly with several mods is refused with a log naming it, and the mods
  themselves keep loading (*Superseded and handed on* §1 records why this replaced "one assembly, one
  mod"). An assembly that declares `[ModContent]` classes but no `[CuoMod]` mod registers nothing and
  says so once, and a declaration owned by a mod discovery can never load — not public, abstract, or not
  constructible with no arguments — is refused with one line per owner, because the registry's candidate
  filter drops such a class without a line of its own.
- Ids must be unique per mod at registration (the existing rail); every id-ordered consumer then sorts by
  the canonical id — tile index allocation, the world-fluid byte and the worldgen streams already depend on
  a stable order.
- A declaration whose member getter throws is refused with a log naming it AND the member, and neither the
  scan nor the other declarations are taken down: the existing "one broken mod never blocks the others"
  policy, one step finer. The scan reads EVERY member of the contract, so a member that computes its value
  is executed at discovery instead of inside a provider's own `Update` where nothing per-declaration can
  catch it. Executing the declaration's code at discovery is expected, not a hazard: mod classes are
  already framework-instantiated (`ModRegistry` requires a public parameterless constructor).
- Null collections keep meaning "none" at the read seam (decision 244's member half).

## Persistence and serialization (what this ticket must not break, and what it makes load-bearing)

- **A definition is code, not data: it is never serialized or stored.** Every peer re-declares it and the
  handshake (mod id / SemVer / permissions / mode) is the consistency boundary — the rule decision 247
  landed. An interface makes that mandatory: a mod-authored implementation may compute its members, so there
  is no stable shape to write down. The save keeps content IDS, and an id whose definition is gone is
  salvaged per entry (`DamageReport.EntryReason.ContentMissing`).
- **CUO's own envelopes stay typed and versioned** (`ModStatusUpdate`, the two projections). The data model
  for a mod's runtime value is `docs/backlog/review/mod-api-no-opaque-envelopes.md`'s subject, not this
  ticket's, and the discriminator is the modification policy's own: a shape CUO owns must be typed, while a
  shape the MOD owns (`IModState`'s value bytes, the mod's half of a status value) is honestly bytes plus a
  `SchemaVersion`, with the mod owning its reader and writer.
- **Per-instance mod data is the part that really has to persist, and this ticket does not invent it.** It
  needs a stable identity across save/load, reconnect and a transfer between players, a version rule both
  ways (older save + newer mod, newer save + older mod), and determinism across peers for anything synced.
  There is no first-class home for it today (mods fake it with their own `IModState` keys), so it waits for a
  named consumer instead of being pre-built here.
- **Computing a definition's values makes the content fingerprint load-bearing.** `SaveManifest.ContentFingerprint`
  is written as `string.Empty` everywhere it is produced today (`WorldCutWriter`), so nothing checks that two
  peers or two saves agree on what the content IS. With code-driven definitions two copies of the same mod
  version can materialize different content — a local config changes a weight — and nothing notices. Opening
  this ceiling therefore owes the fingerprint a meaning: a hash of the materialized content, reported as a
  named mismatch instead of silently accepted.
- **Keep the default implementations plain and settable.** The framework reads the interface; a future
  data-driven loader (a JSON content pack) would deserialize into the concrete default, which is why its
  members stay public `{ get; set; }` with no required constructor arguments. That is also why the deleted
  `[DataContract]` attributes are not missed: a modern serializer needs no attribute when the members are
  public and settable.

## Settled at implementation (2026-10-09)

- **The scanned instance is stored as it is**, decision 247's rule, and nothing is snapshotted: the scan
  reads every member once (so a getter that throws is refused there) and then hands the object over, which
  is what lets a member that computes its value be re-read when the game table is rebuilt. A snapshot
  would freeze the first read and make the second half of the ticket's own acceptance row impossible.
- **Registration runs BEFORE `ICuoMod.Bind`**, and a declared definition is therefore registered ahead of a
  code registration of the same id: the declared half is the mod's static content, the code half is what it
  computes at bind time, and the existing duplicate rail refuses the later one.
- **The content fingerprint left this ticket** (`todo/mod-content-fingerprint.md`), and so did the nested
  member contracts (`review/mod-content-nested-member-contracts.md`) — see *Superseded and handed on*.

## Non-goals

- No second registry, no second permission rail, no second binder.
- No framework type required to declare content: the nine DTOs are implementations of the kind interfaces,
  not the contract, and the code path keeps accepting either.
- No persistence of definitions, and no second data model for a mod's runtime values: definitions are
  re-declared by code on every peer, runtime values stay with the sweep ticket.
- **No registration of the game's own content.** The vanilla tables belong to the game and are rebuilt by it
  (`Item.SetupItems`, `Recipes.SetUpRecipes`, `WorldGeneration.tiles`); CUO only PROJECTS them read-only into
  the same address vocabulary — `VanillaItemResourceLocationSource` turns `Item.GlobalItems` into
  `cu:<item id>` entries in the same `ResourceLocationCatalog` the mod content feeds, and deliberately skips
  ids a mod injected. Registering the vanilla set as definitions would invert ownership (a per-mod registry
  holding views over tables the game rewrites), and the binder would re-materialize hundreds of entries that
  are already there.
- **Any content CUO itself ships goes through the same `TryRegister` path as a mod's** — never a private
  channel. That is the dogfooding this architecture needs; the game's own content is not CUO's to register.
- No change to the wire or the save: content is process-local either way.
- No auto-loading of assemblies that declare no mod.
- No compatibility shim: the code path and the scan are the same contract, so neither is kept alive for the
  other.

## Acceptance (mod-author visible, judged at the acceptance batch)

- A mod declares an item and a recipe by attribute only, from ITS OWN classes implementing the kind
  interfaces (no framework DTO instantiated anywhere), with no registration call, and both materialize in
  game tables.
- A declaration whose members COMPUTE their values (a weight that depends on the mod's own configuration, not
  a constant) materializes with the computed values, and they are re-read when the game table is rebuilt —
  the same definition feeding a fresh world can carry different numbers.
- A mod-authored nested implementation (its own `IModItemTool`, say) materializes with that tool's values:
  the seam is the interface, not the framework's class. **Moved verbatim** to
  `docs/backlog/review/mod-content-nested-member-contracts.md`: the contracts it names do not exist, so this
  ticket cannot judge it.
- The same two definitions registered through code, as `ModItemDefinition` instances, still materialize —
  one provider, two ways to feed it.
- A class that reaches two kinds is refused with a log naming it; the other declarations still bind.
- A declaration whose getter throws is refused, named, and does not stop its siblings.
- Two mods declaring the same bare id still report the existing conflict; the scan adds no new id rule.
- An assembly without `[CuoMod]` that declares content by attribute alone binds nothing (and says so), while
  the same definitions registered through code bind.
- Owed by the fingerprint rule above: two clients that materialize different content under the same id and
  the same mod version are reported as a named mismatch rather than silently accepted. **Moved verbatim**
  to `docs/backlog/todo/mod-content-fingerprint.md`: nothing consumes a fingerprint today, so it is a
  save-side ticket rather than a row of this one.
