# A null collection in a content payload must mean "none"

- Status: Review — the development half landed 2026-10-07 (see *What landed*); every acceptance row below is
  judged by this repository's own suite, because all of them are load-time and client-local. The rows that
  would need the game running are named in *Limits*.
- Priority: Medium
- Category: Mod platform / content surface
- Source: found by the independent review of `review/mod-crafting-quality-labels.md` (2026-10-07), which
  asked why an explicitly-null `Qualities` list made the definition fail. The answer is a payload-wide
  pattern, not one field: `DataContractSerializer` runs no property initializer, so a mod that assigns null
  to a list round-trips an explicit nil, and every provider that dereferences such a list without a guard
  turns "I did not set this" into a failed definition.
- Related: `review/mod-crafting-quality-labels.md` (fixed the two sites it touched and records the
  remaining sweep here), `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md` (the DTO tables
  and the rule), `docs/decisions/active.md` (entry 244),
  `docs/evidence/selfchecks/mod-api/mod-payload-null-collections-selfcheck.md`,
  `docs/backlog/todo/mod-content-ceiling.md` (the surface this belongs to)

## What is asked

One rule for every mod-authored DTO collection: **null means empty**, decided where the payload is decoded,
not by each consumer. Today it is decided per consumer and unevenly.

## Evidence (measured 2026-10-07, before this cycle)

- **A member can be null two ways**, and a `DataContractSerializer` probe against the built Abstractions
  assembly settled both: it runs **no constructor and no field initializer** (0 constructor calls during
  deserialization), so (a) an explicit `i:nil` reaches the member's property setter — 1 call, carrying null —
  while (b) a member whose element the payload **omits** is never set at all and stays null. The member's
  initializer covers neither case on a decoded object; it only covers a mod's own `new` in code.
- The trap was pinned for the nil shape: a test asserted that a definition built with a null `Qualities`
  list comes back with a null list, which is why the two providers that read it normalised it
  (`definition.Qualities ??= []`). Its replacement,
  `ModItemDefinitionTests.NullCollectionMembers_MeanNone`, pins the rule instead.
- The members whose consumers dereferenced them with **no guard at all** are both named `SpawnComponents`:
  `CustomItemTemplateFactory` and `CustomBuildingTemplateFactory` hand it to `CustomComponentAttach.Attach`,
  which iterates it, and each provider's own success line reads `definition.SpawnComponents.Count`. A null of
  either shape threw `NullReferenceException` at all four call sites.
- Those four sites are on the runtime-template path inside the provider's `Update`, not in `TryBind`, and that
  path's failure is a per-frame error rather than a skipped definition: `Plugin.RunLifecycle` wraps each
  `ICuoService.Update` and logs `ICuoService.Update failed for {ServiceType}`, and because the throwing
  template is never recorded in the provider's failure set the same definition throws again on the next
  frame — with every later definition in that frame's loop skipped alongside it.
- Every other member was guarded, but per consumer and unevenly: `DropOnDestroy` / `AlwaysDrop` /
  `LimbMoodles` sit behind an `is not null` or `is { Count: > 0 }` pattern, `Drops` is read through that
  pattern in one place and `?? []` in another in the SAME file, `Rows` / `VanillaBlocks` / `TileIds` /
  `SpawnCounts` / `Ingredients` / `Qualities` are normalised at the top of one provider and not at all in the
  next, and `TagRestriction` / `SwingSounds` / `Value` / `Payload` each carry their own `?.` or `?? []`.
  A guardless dereference **inside** `TryBind` would have been caught by the binder
  (`provider for {Kind} threw while binding {ModId}/{Id}`) and cost the whole definition; this tree had none
  there, and the two shapes above are what the guards and the missing ones actually decided.

## What landed

> **[Narrowed again 2026-10-09 by the typed value model.]** The DECODE half of this rule and the census
> that drove it are RETIRED, not moved: `ModStatusUpdate` and the two status projections were the last
> contracts CUO carried over a boundary as serialized objects, and they carry a `ModValue` now, so
> `ModPayloadCodec`, the `[DataContract]` attributes and the three payload shapes this ticket's rows 1-4
> measured no longer exist (ticket `todo/mod-api-no-opaque-envelopes.md`, stage A). What is left is the
> MEMBER half, and its census is `ModNullCollectionRuleTests` — 26 rows, one fewer than the 27 below,
> because the runtime moodle request's `Payload` is a `ModValue` now and a value is not a collection. The
> value model's own read-only `Items`/`Fields` views are a NAMED group in that census rather than rows.

> **[Narrowed 2026-10-08 by decision 247.]** The counts below are this cycle's record. A content definition
> is a registered typed object now and is never serialized, so the decode seam reaches only the contracts
> CUO itself carries over a boundary (`ModStatusUpdate` and the two status projections) and the census is
> one payload member driven through three shapes plus 27 mod-built declarations driven with a null write.
> The MEMBER half below is unchanged and is what every mod-built declaration answers for.

- **The rule is answered at both ends of a payload.** Every collection member of every mod-authored contract
  in `CasualtiesUnknownOnline.Abstractions` is a field-backed property whose setter coalesces null
  (`public List<T> X { get; set => field = value ?? []; } = [];`): 27 members across the payload-contract graph
  (26 `[DataContract]` types, of which 12 own a `ToPayload`/`FromPayload` pair), plus the five declarations a
  mod builds in code (`CuoModAttribute.Dependencies`, `ModConsoleCommand.ArgumentKinds`,
  `ModManifest.Dependencies`, `ModPacket.Handlers`, `ModStatusMoodleRequest.Payload`; the middle two already
  coalesced in their constructors). That answers a mod that writes null in code and keeps a payload we build
  free of explicit nils.
- **A decode seam answers the shape a setter cannot see.** `ModPayloadCodec` is now the one decode step behind
  all twelve `FromPayload` methods (which shrink to a one-line call, deleting twelve copies of the same
  try/catch): it deserializes and then replaces every null collection member of the decoded graph with an
  empty one, recursing into nested contracts and collection entries. This is what closes the omitted-element
  shape — the serializer runs no constructor and no initializer, so a member with no element has no other
  owner — and it means a decoded definition is *repaired*, not merely hidden: encoding it again writes an
  empty collection rather than a nil.
- **Why the member alone was not enough** (the first cut of this cycle, corrected by the independent review):
  the member's setter sees a nil but never sees an absent element, so the two shapes need the two ends. The
  ticket's "no single decode seam" reading was wrong for the absent shape — every root does own a
  `FromPayload`, which is exactly where the second half belongs. Recorded as decision 244.
- **Every consumer-side guard on those members is deleted in the same change**, so no provider decides the
  rule: the `??= []` in the item, liquid and recipe providers, the `?? []` in the structure and tile
  providers, the two `is not null` wrappers in the building provider, the `?.ToArray() ?? []` on
  `SwingSounds` and `TagRestriction`, `CustomBuildingTemplateFactory`'s two helpers tightened to
  non-nullable parameters, `ModRegistry`'s `attribute.Dependencies ?? []`, and
  `ModStatusDefinition.ResolveMoodleId` / `ModStructureDefinition.TryGetSpawnCount` losing their null
  clauses. `is not { Count: > 0 }` becomes the emptiness check it always meant. One consumer keeps a
  coalesce that is not about this rule: `ModStatusMoodleProjection` fills the runtime moodle request from the
  status store's own `byte[]?` out value.
- **A member that is genuinely required is not an exception**: an empty collection flows into that
  provider's own validation, which refuses the definition with the reason it names (an item or moodle
  animation with no frame paths, a recipe with no ingredients, a structure whose grid has no rows).
- **The sweep is a census, not a list**: `ModPayloadNullCollectionTests` discovers every collection member
  of every `[DataContract]` in the assembly and drives all three shapes for each one (our own payload, an
  explicit nil, an omitted element) plus the re-encode check, with a pinned member count and an
  assembly-wide equality assertion; `ModContentNullCollectionBindingTests` drives the real content binder
  through all nine GameAdapter providers.
- The `Qualities` test that pinned the trap is replaced by
  `ModItemDefinitionTests.NullCollectionMembers_MeanNone`: it pins the rule instead.
- Docs: the rule on the mod API page in both blocks (with the pair hashes re-recorded), decision 244, the
  cycle's self-check, this ticket.

## Acceptance

| # | Row | How it is judged | Result |
|---|---|---|---|
| 1 | Every collection member of every mod-built declaration means "none" when a mod assigns null in C# | `ModNullCollectionRuleTests.NullCollectionWrite_IsNone`, one row per member (26) | pass |
| 2 | The census reaches every collection member of the assembly, so a new member cannot arrive without the rule | `ModNullCollectionRuleTests.Census_CoversEveryModAuthoredDeclarationTheAssemblyBuilds`: pinned count (26) plus equality with the assembly scan | pass |
| 3 | The declarations a mod can only build through a constructor follow the same rule | `ModNullCollectionRuleTests.ParameterObjectDeclarations_TreatNullAsNone` | pass |
| 4 | A definition with every collection member null still BINDS | `ModContentNullCollectionBindingTests`: item, liquid, liquid tile, building, tile, status, moodle, and the structure with a grid | pass |
| 5 | A member that is genuinely required is refused with the message that says why, never with the binder's logged exception | the same suite: a frame-less item animation, a recipe with no ingredients, a structure with no rows, a frame-less moodle animation — each read from the provider's own warning, with no error entry on the binder | pass |
| 6 | No provider normalises a collection any more | the guard sweep in *What landed*; the census plus the binder suite fail if the rule is lost | pass |
| — | *(retired)* rows 1-5 of the previous revision: an explicit nil in a payload, an omitted element, a null write, a payload we build staying free of nils, and the payload-member census | the contracts they measured are gone — a status value is a `ModValue` and CUO encodes it, so there is no decoded object graph and no nil to normalise (ticket `todo/mod-api-no-opaque-envelopes.md`, stage A) | retired |

## Non-goals

- Not a general "tolerate anything" pass: a member that is required keeps its refusal, and a member that is
  invalid keeps its validation.
- Not a change to any payload shape: this is about what an absent or nil member means, not about adding or
  removing fields.

## Limits

- The provider paths that need Unity objects — runtime template construction, `CustomComponentAttach.Attach`,
  the `Resources.Load` animation frames — are not executed by the suite. The rule is proven at the boundary
  every path shares (the member and the decode seam), and no production code can hand a provider a
  collection member that reads null.
- `ModContentDefinition.Data` is carried as an accounted-for exception in the census rather than changed: it
  is the registry's defensive copy of bytes the registration policy already refused when null, so its
  accessor cannot answer null either.
- The census scans public CLASSES of Abstractions; an interface member is a read-only view the framework
  answers, not a declaration a mod fills in (16 of them are outside the census). The framework's own file
  formats (`ModStateFile`, `HostBanFile`) are outside the mod payload contracts.
- Nothing here was observed in a running game, and nothing here needs to be: every row is load-time and
  client-local. A session would only re-confirm that content still appears, which the existing
  content-binding acceptance rows already cover.
