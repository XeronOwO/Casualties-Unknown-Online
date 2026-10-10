# The mod item's wearable declaration: the placement the game's own wear flow reads

Scope: ticket `docs/backlog/review/mod-item-wearable-declaration.md`, cut 2026-10-11 out of
`docs/backlog/todo/mod-content-ceiling.md` Part 3.A ("the wearable set"). One declared flag
(`IModItemDefinition.Wearable`) reached the game's own wear flow with no placement behind it, and that
family dereferences the limb it could not resolve — so this cycle closes a crash of a surface the API
already promised, then gives the surface the data the game actually reads. The independent FULL-tier review
(`.agent-local/reviews/mod-item-wearable-declaration-review.md`, fresh context, frozen tree) raised no
blocker; its three majors and its minors were fixed in the same commit and their fixes are rows 7, 8, 13 and
14 below.

## §1 Mechanism inventory

| # | Mechanism | What changed | Evidence |
|---|---|---|---|
| 1 | `IModItemWearable` + `ModItemWearable` (new, Abstractions) | the declaration itself: `Limb` and `SlotId` (the placement), `CanBeHeld`, `Armor`, `Isolation`, `HitDurabilityLossMultiplier`, `VisualOffset` (defaulting to the game's own field initialiser, 5) | `src/CasualtiesUnknownOnline.Abstractions/IModItemWearable.cs`, `ModItemWearable.cs`; `ItemWearableDeclarationTests.Item_DeclaringAPlacement_MapsEveryFieldTheGameReadsToPlaceAndWearIt` asserts all seven on the built `ItemInfo`, `….Item_DeclaringOnlyAPlacement_KeepsTheGamesOwnDefaultsForTheRest` pins the defaults |
| 2 | `IModItemDefinition.Wearable` / `ModItemDefinition.Wearable` | `bool` → `IModItemWearable?`: the flag can no longer travel without the data it gates, because there is no member left that carries only the flag | `src/CasualtiesUnknownOnline.Abstractions/IModItemDefinition.cs`, `ModItemDefinition.cs`; the API baseline moved by 16 added entries and 2 changed signatures; the five test doubles and the example mod follow |
| 3 | `WearableDeclaration.HasPlacement` (new, GameAdapter) | the ONE rule behind "can the game place this garment": both halves of the placement non-blank, asked by the mapping and by the load-time report | `src/CasualtiesUnknownOnline.GameAdapter/Content/WearableDeclaration.cs`; `ItemWearableDeclarationTests.Item_DeclaredWearableWithNoPlacement_IsNotMarkedWearableAndIsReported` (red first, see §4) |
| 4 | `ModItemInfoFactory.Build` | installs `wearable` and `desiredWearLimb` + `wearSlotId` + the five statistic/visual fields TOGETHER, and only for a placeable declaration | `src/CasualtiesUnknownOnline.GameAdapter/Content/ModItemInfoFactory.cs`; the two cases above; the red was `Assert.False() Failure / Expected: False / Actual: True` on the built flag |
| 5 | `GameAdapterItemContentProvider.TryBind` | an incomplete wearable declaration is ACCEPTED, registered WITHOUT the flag, and reported by name at load — the sibling ticket's ruled shape (materialize-and-report) applied where "materialize" can only mean "the item still exists, the half that cannot be backed is off" | `src/CasualtiesUnknownOnline.GameAdapter/Content/GameAdapterItemContentProvider.cs`; the warning assertion in the same case |
| 6 | `CustomItemBehaviorValidator.ValidateWearable` | negative `Armor` / `Isolation` / `HitDurabilityLossMultiplier` are refused with the rest of the item's numeric validation (they would invert damage absorption and cold protection) | `src/CasualtiesUnknownOnline.GameAdapter/Content/CustomItemBehaviorValidator.cs`; `ItemWearableDeclarationTests.Item_DeclaringANegativeWearableNumber_IsRefused` |
| 7 | `GameWearPlacement` (`src/CasualtiesUnknownOnline.GameAdapter/Content/GameWearFacts.cs`) | the limb-name half of the placement, extracted so every reader asks ONE rule: `TryResolveLimbIndex` (the body's own limb names), `TryIndexOfLimbName` (the comparison itself, split out so it is pinnable without a live body), `IsPlaceable` (the chain's verdict) and `Refuses` (the guard family's verdict, for an id and for an `ItemInfo`). `TryResolve` keeps its answer for the chain, so `WearTests` stays green | the limb-name rule is pinned by `ItemWearableDeclarationTests.TheLimbNameRule_MatchesTheGamesOwnComparison` (6 cases: match, non-first match, case mismatch, empty, absent, first-wins) through the internal type by reflection |
| 8 | The guard family | `BodyPatches.WearWearablePatch`'s prefix refuses the wear, and the new `Patches/WearableQueryPatches.cs` refuses the three sibling QUERIES — `GetWearable(string)` (answers "no worn item"), `HasWearable(string)` and `HasWearable(Item)` (answer "not worn") — because the `LimbByName` census of the game assembly is exactly those four sites (`Body.cs:1493`, `:1541`, `:1558`, `:1575`) and a `wearable` flag selects all of them. Every guard asks `GameWearPlacement` | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`, `Patches/WearableQueryPatches.cs`; `AdapterCapabilityCatalogTests.EveryPatchClass_IsClaimedByExactlyOneCapability` (the new class is registered under `Character`); the display-proxy redirect re-enters the same patched method on the body the ring shows, so its answer is refused there too, whichever prefix runs first |
| 9 | `IWearablePatchPort` (new port) + `PatchBridge.Wearable` + `GameAdapterBridge.ReportWearPlacementRefused` | the guards' report. `IPatchBridge` is frozen (a member may not be added there), so the one thing a static patch cannot reach — the logger — arrives through a one-member port, the shape `ILayerAdvancePatchPort` already has | `src/CasualtiesUnknownOnline.GameAdapter/IWearablePatchPort.cs`, `PatchBridge.cs`, `GameAdapterBridge.cs`; `PatchBridgePortShapeGateTests` (pins updated: seam census, served ports, static accessors) and `PatchBridgePortContractTests` (3 new cases: the port's surface, the aggregate does not declare it, the bridge implements it) |
| 10 | `WearWearablePatch`'s `__state` | a three-value `WearOutcome` (`Refused` / `WorldItem` / `CarriedItem`) instead of a bool, because Harmony runs the postfix even when a prefix skipped the original: a refused wear must not send the "inventory changed" snapshot the postfix owes a wear that landed (review finding F2) | `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`; the postfix returns on `Refused` before its proxy guard and before `OnInventoryChanged` |
| 11 | The pages a reader meets | the item DTO row gained `Wearable` in the behaviour list and the placement sentence, in BOTH languages; a new rule paragraph ("A wearable needs a placement" / "可穿的衣物必须带落位") states the rule, the refusal and the guard; the alignment record re-recorded both hashes | `docs/en/reference/mod-api.md` + `docs/zh/reference/mod-api.md`, `docs/standard/alignment.txt`; `git hash-object` on both pages equals the recorded pair |
| 12 | The records that were wrong | the ceiling ticket's Part 3.A entry names this ticket and the crash, and its two stale sentences about the wearable set are corrected; the sibling ticket's non-goal ("none of them is a crash") is corrected in place with the native evidence, because it is the claim that hid this defect | `docs/backlog/todo/mod-content-ceiling.md`, `docs/backlog/review/mod-declared-behaviour-with-no-function.md` |
| 13 | The tests | one new file, 10 cases (4 on the declaration, 6 on the limb-name rule); 3 new port-contract cases; 5 test doubles and the example mod follow the contract change | `tests/CasualtiesUnknownOnline.Tests/Patching/ItemWearableDeclarationTests.cs` (new), `PatchBridgePortContractTests.cs`, `ModContentDeclarationScannerTests.cs`, `ModAuthoredDefinitionBindingTests.cs`, `ItemAdvancedBehaviorProviderTests.cs`, `TestDeclaredContentMod.cs`, `src/CasualtiesUnknownOnline.ModExample/ExampleMod.cs` |

## §2 Whole-family audit

- **The crash family was bounded by a census, not by a guess.** Every `LimbByName` call in the game assembly
  was read (`Body.cs:1467` declares it; `:1493`, `:1541`, `:1558`, `:1575` dereference the result with no
  null check; `ConsoleScript.cs:1058`/`:1087` are developer-console commands whose argument the console
  supplies; `Wearable.cs:19` is the multi-worn-sprite path). The four `Body` sites are the family this cycle
  guards — the first one by the wear prefix, the other three by `WearableQueryPatches`.
- **The caller list is four, not two** (the ticket said two before the review): the radial centre drop, the
  touch auto-wear, a save load (`SaveSystem.cs:338`) and CUO's own `RemoteIntentApplier.ApplyWearItem`. The
  query sites are reached from the drag release (`PlayerCamera.cs:1698`), the drop report and
  `Body.DoPickupCheck` (`:1358`), which is why guarding only the wear left the same defect reachable.
- **The multi-worn-sprite sibling was checked and needs no guard of its own.** `Wearable.CreateSprites`
  (`Wearable.cs:19`) dereferences the same lookup for `IModItemLimbWornSprite.LimbName`, and CUO's own
  `CustomItemVisualPatches` prefix filters those entries against the LIVE body
  (`CustomItemVisualState.ConfigureWearableSecondarySprites`) before the native body runs on every call —
  the template-time call passes a null body and leaves the names unfiltered, which is why the filter exists
  at wear time. A prefix runs for every invocation of the patched method, so no path reaches the dereference
  with an unknown name.
- **The declaration family was walked, not just the reported case.** "A declaration the framework cannot
  materialize" has now been closed four times by name (`mod-content-kind-with-no-provider`,
  `mod-crafting-quality-labels`, `mod-declared-behaviour-with-no-function` and this one). The sibling's shape
  was checked and followed: the declaration is NOT fatal — the item keeps existing, the half that cannot be
  backed is inert, and one log line names it. The difference is recorded rather than papered over: there the
  framework could install an inert delegate; here it cannot install an inert limb, so the inert form is "no
  wearable flag at all".
- **Every field the vanilla wear flow reads is either mapped or named as out of scope.** The seven fields
  Part 3.A lists are all mapped (the table in the ticket and in §1 rows 1/4); `rec`, `onlyHoldInHands`,
  `combineable`, `ignoreDepression`, `scaleWeightWithCondition`, `jumpHeightMultChange` and `slotRotation`
  are the ceiling ticket's OTHER Part 3.A entry with their own consumers, and the ticket's non-goals say so.
- **The placement rule has three readers, and the third one was checked too.** The cross-player chain
  (`TryResolve` → `IWearSemantics` → `WearAdmission`), the four guards, and `WearableRestorer` on the
  affected side — which does NOT read the limb name at all (it restores from the encoded limb INDEX the
  snapshot carries) and therefore needs no half of this rule.
- **The limb vocabulary was reused, not reinvented.** `IModItemLimbWornSprite.LimbName` and
  `IModLimbMoodleBinding.LimbName` already publish "the same name the game's `Body.LimbByName` lookup
  resolves", so a CUO enum was rejected: it would be a second vocabulary to keep in step with a prefab
  neither side can read statically — and the comparison is the game's own exact one, with no case folding
  (`Body.cs:1471`), which is why the rule's cases pin the case mismatch as a refusal.
- **No wire change and no protocol bump.** Content is materialized locally from the mod each peer already
  has; the handshake's per-mod content fingerprint compares addresses (decision 254) and does not move for
  this change. Decision 241 keeps `ProtocolVersion.Current` frozen at the pre-release baseline, and nothing
  here changes a message.

## §3 What landed, and what deliberately did not

Landed: the wearable declaration as one typed slice with the placement inside it, the mapping that installs
the flag and the data together, the refusal of an incomplete declaration with a load-time report, the
numeric validation, the shared limb-resolution rule with its comparison pinned at L0, the guard family over
all four native dereference sites with its patch port, the postfix that no longer reports a wear that did not
happen, the tests, and the records that were wrong.

Deliberately not:

- **No CUO limb enum or index in the DTO.** The game reads a limb NAME (`Body.LimbByName`) and the API
  already publishes that vocabulary; an enum would be a CUO-owned copy of a prefab fact, and an index would
  be the prefab's own order, which is exactly the fragility `GameWearFacts` documents.
- **No load-time check against a body.** A limb name can only be validated against a live body, and mod
  content is injected the moment `Item.GlobalItems` exists (`Item.SetupItems()` runs inside
  `WorldGeneration.cs:124`, before a player body is guaranteed) — so a load-time answer would be a guess
  about scene timing, and a re-check that waits for the body would be a window no gesture may rely on. The
  guards answer where the game asks.
- **No mutation of the item's own data by the guards** (no "self-heal" that clears the flag on first
  refusal). It would make a repeat impossible, but it writes into the game's own content table while a
  gesture is in flight, and the sibling ticket's precedent is a report per event rather than a silent repair:
  a broken declaration stays visible in the log every time it is met.
- **No repair of a bad limb name.** A case-mismatched or misspelled name is refused and logged, never
  rewritten to a limb that "looks close": the item's own declaration is the only authority for where it goes,
  and a fallback here would be the fallback defect the entry-mapping rule names.
- **Not the limb action** (`usableOnLimb` / `useLimbAction`) — its consumer is a delegate `Abstractions`
  cannot carry, so exposing the flag alone would add a second crash of this shape; that surface is
  `docs/backlog/todo/mod-authored-effects.md`.
- **Not the miscellaneous Part 3.A reads**, and **not native's drop-the-occupant behaviour** on a wear into
  an occupied slot (the cross-player chain records that limit; it needs a world-drop fact this path does not
  have).
- **No new behaviour beyond the promised surface**: a mod garment can now be worn and carried between
  players, which is what the ceiling ticket's Part 3.A and the user's 2026-10-07 promotion directive asked
  for; the wear chain already reads the item's own data and needed no change for it.

## §4 Verification

| Layer | Command | Result |
|---|---|---|
| Red, before the fix | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ItemWearableDeclarationTests"` on the tree as it stood before the change | 1 failed / 0 passed: `Item_DeclaredWearableWithNoPlacement_IsNotMarkedWearableAndIsReported` — `Assert.False() Failure / Expected: False / Actual: True` at the built `wearable` flag, i.e. a declaration that names no placement installed the flag the game's own flow dereferences. The case's SETUP line had to follow the contract (a blank `ModItemWearable` where the old contract could only write `true`), so the red is recorded here and is not reproducible from the committed test — see §5 |
| Build | `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings, 0 errors |
| Focused (the touched areas) | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~CasualtiesUnknownOnline.Tests.Patching\|FullyQualifiedName~CasualtiesUnknownOnline.Tests.Mods"` | 964/964 |
| The new suite | `--filter "FullyQualifiedName~ItemWearableDeclarationTests"` | 10/10 (4 declaration cases + 6 limb-name cases) |
| The port's contract | `--filter "FullyQualifiedName~PatchBridgePort"` | 35/35 (22 shape-gate cases + 13 contract cases, 3 of them new) |
| The capability catalog | `--filter "FullyQualifiedName~AdapterCapabilityCatalogTests"` | green with `WearableQueryPatches` registered under `Character` |
| Behaviour (whole suite) | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` | 4913/4913 (was 4900; the 13 new cases are the 10 + 3 listed in §1 row 13) |
| Normative gates | the same run's `CasualtiesUnknownOnline.NormativeGates.Tests` | 573/573 with the checklist gate excluded (mid-cycle it is red by design — the boxes are unchecked until the cycle closes); the full gate run with every box checked is the pre-commit run |
| Format | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0; every changed file re-measured byte-wise for CRLF |
| The API baseline | `--filter "FullyQualifiedName~ApiSurfaceGateTests"` | green after reviewing the candidate: 16 added entries (the new interface, its implementor and their members) and 2 changed signatures, no tombstone — the member KEY survives, only its signature moved, which the gate reads as a live entry (a tombstone for a live key is itself a red, and was observed) |

## §5 Limits

- **The native half needs a game process.** Whether the guards really stop each call before the game
  dereferences it, whether a declared garment lands on the right limb of the right body, and what the
  third-party view shows are acceptance rows. L0 pins what the provider CONSTRUCTS and the rule the guards
  ask; it cannot build a `Body`.
- **The red is not reproducible from the committed tree.** The case's setup had to change with the contract
  it exercises (the old contract could only write `Wearable = true`), so the recorded failure belongs to the
  tree as it stood before the change — the same situation `mod-declared-behaviour-with-no-function.md`
  records for its own split build site. What reproduces from this tree is the case's ASSERTION, green.
- **The guards refuse a superset of the throwing state.** A wear whose limb cannot resolve is refused even
  where an earlier native guard (an occupied slot's `alertalreadywearing`, `DoPickupCheck`) would have
  returned before reaching the dereference. For vanilla content nothing changes — every vanilla wearable
  names a limb every body carries — and for content that cannot be worn at all, "not worn" is the defined
  answer that the load-time half already installs for an incomplete declaration. Recorded rather than
  narrowed, because reproducing those earlier guards inside the prefix would duplicate the game's own order.
- **A limb name that exists on another game build is not caught.** One session runs one game build, so the
  prefab's limb names are the same everywhere in it; a name that is valid in a DIFFERENT build is refused by
  the guards on every body of this one, and reported.
- **The comparison is exact, like the game's own.** `Body.LimbByName` compares with `==`
  (<c>Body.cs:1471</c>), so `head` does not match `Head`; CUO refuses that wear with a log rather than
  normalising the case into the field the game reads — the rule's own case pins the refusal.
- **The guards' refusal is a log line, not a player-facing alert.** The native refusals beside them alert
  through locale strings whose arguments name a limb short name this state does not have; the refusal is
  therefore observable in the log, not on screen. The `GetWearable`/`HasWearable` guards answer with the
  meaning the query has for an item that cannot be worn, so the game's own callers continue down their
  ordinary "not worn" branches.
- **A third-party plugin writing `Item.GlobalItems` directly is outside the content API's promises** — the
  guards still cover the crash state for it, which is more than the API owes it.
- **`VisualOffset`'s DTO default is 5**, mirroring `ItemInfo.wearableVisualOffset`'s own initialiser, so an
  unauthored declaration keeps the game's sorting order; a mod that wants a different one states it.
- **The numeric validation refuses the whole definition**, not just the wearable slice — the validator's
  existing semantics for every other behaviour DTO, kept rather than special-cased here.
- **`GameAdapterBridge` is now 553 lines** (was 548): the class is the reviewed aggregate that serves every
  patch port and every member is a one-line forward to its domain, so its length is the seam count rather
  than a responsibility; the hard structure gate is 600 lines and the port shape is pinned by its own gate.
  Recorded because it crossed the local "measure before 550" line in this change.
