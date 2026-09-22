# Architecture Guards

Active enforcement rules for the typed deterministic kernel. The list is the
authoritative set of kernel-shaped invariants. Not every item is automated today:
five are implemented as C# gates in `SourceShapeGateTests`, while the rest are
currently covered by tests/processes and remain aspirational for tooling.

## Mandatory guard list

1. **GameState isolation**: `CasualtiesUnknownOnline.GameState` must not reference
   Unity, Runtime, Protocol codecs, network packages, BepInEx, or Steam.
2. **Domain isolation**: Domain A must not reference Domain B's internal namespace.
3. **Wire-free domain surface**: protobuf/wire DTOs must not appear in domain public
   interfaces.
4. **No Unity in kernel data**: Unity types must not appear in Command/Event/Checkpoint.
5. **Event completeness**: every Event type must be registered with a reducer and a
   serialization contract.
6. **Authority policy completeness**: every Command must declare an Authority Policy.
7. **Checkpoint completeness**: every persistent domain field must be included in
   checkpoint round-trip tests.
8. **Invariant suites**: key aggregates must register invariant suites run by tests and
   debug/traces.
9. **No generic core state**: string event names and `Dictionary<string, object>` core
   state are prohibited.
10. **No silent legacy**: `Legacy`/double-write code must carry a deletion milestone and
    must be zero before Phase E ends.

## Automation status

| Guard | Automated today? |
|---|---|
| 1 GameState isolation | ✅ `SourceShapeGateTests.GameStateIsolation_NoForbiddenReferencesOrTokens` |
| 2 Domain isolation | ⚠️ Not covered by a standalone C# gate yet |
| 3 Wire-free domain surface | ✅ covered by GameState isolation C# gate (partially) |
| 4 No Unity in kernel data | ✅ covered by GameState isolation C# gate |
| 5 Event completeness | ❌ not automated; test/process coverage |
| 6 Authority policy completeness | ✅ `SourceShapeGateTests.CommandAuthority_EveryGameCommandDeclaresAuthority` |
| 7 Checkpoint completeness | ❌ not automated; test coverage |
| 8 Invariant suites | ❌ not automated; test coverage |
| 9 No generic core state | ✅ `SourceShapeGateTests.KernelShape_NoStringKeyedStateOrHashtable` |
| 10 No silent legacy | ✅ `SourceShapeGateTests.NoLegacy_NoRemovedDualArchitectureMarkers` |

## Suggested tooling shape

- A project reference / namespace analyzer run during build.
- A reflection or source-scan pass in `SourceShapeGateTests` or a successor.
- Golden serialization tests for every wire Event and checkpoint schema.
- A registry test that enumerates Commands/Events and verifies required metadata.
- A dependency-direction test that fails if `GameState` pulls in a forbidden project.

## Phase-specific guard additions

| Phase | Guards to activate |
|---|---|
| A | `GameState` isolation, no Unity in kernel data, basic invariant tests for Item shadow. |
| B | Wire-free item domain surface, all item Commands declare authority, item checkpoint round-trip, capability registry completeness. |
| C | Event serialization contract for all new wire Events, envelope versioning, golden wire tests. |
| D | Domain isolation across all migrated domains, checkpoint completeness, invariant suites for all key aggregates. |
| E | Full 10-item guard list as the target; the five C#-checked guards are wired into `SourceShapeGateTests`, the rest are test/process-covered. |

## Relationship to existing gates

The existing repository gates (600-line classes, state bool limits, one top-level type
per file, event-replay matrix, entity-event dispatch) remain in force. The guards above
are additions for the new deep architecture, not replacements.

## Landed guard automation

Guard 1 (GameState isolation) is automated by
`SourceShapeGateTests.GameStateIsolation_NoForbiddenReferencesOrTokens`. It rejects
CUO project references, Unity/BepInEx/Steam/network packages, raw assembly
references, forbidden source namespaces, ambient random/wall-clock usage, and
(Phase B) Protocol DTO/protobuf tokens in
`src/CasualtiesUnknownOnline.GameState`.

Phase B addendum: `SourceShapeGateTests.ItemAuthority_NoDirectProjectionMutation`
rejects direct mutations of `WorldItemTable`/transfer-table state outside the item
projection classes, so old tables stay rebuildable projections.

Phase E addendum: `SourceShapeGateTests.NoLegacy_NoRemovedDualArchitectureMarkers`
rejects dual-architecture type declarations (`Shadow`/`Legacy`/`Compat`/`Dual`)
plus known removed direct-result/legacy wire markers in `src/`.

Phase E addendum:
`SourceShapeGateTests.CommandAuthority_EveryGameCommandDeclaresAuthority` requires
every `GameCommand` subclass in the GameState kernel to carry an
`AuthorityKind`/authority policy.

Phase E addendum: `SourceShapeGateTests.KernelShape_NoStringKeyedStateOrHashtable`
rejects string-keyed dictionaries or `Hashtable` state in the GameState kernel.

Application-layer addendum:
`ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection` enforces the declared project
direction — GameState, Protocol and Abstractions reference no project, Application is the only path
from Runtime up to the kernel and may reference GameState/Protocol only, and GameAdapter/Plugin never
reach GameState directly — over the graph read from `CasualtiesUnknownOnline.slnx` (project
references AND raw CUO assembly references), with a census floor, a CLASSIFICATION census (every
solution project must be declared in the layer table or listed as a consumer, so nothing is exempt by
omission) and every declared project checked for presence; its synthetic cases
(`...GameStateReferencingUpward_IsRefused`, `...RuntimeReachingGameStateWithoutTheLayer_IsRefused`,
`...ApplicationReferencingUpward_IsRefused`, `...PluginReachingGameStateDirectly_IsRefused`,
`...ApplicationReferencingItsLowerLayers_IsAccepted`, `...ConsumersAreNotConstrained`,
`...UndeclaredProject_IsRefusedInsteadOfSilentlyExempt`, `...AbstractionsReachingDown_IsRefused`) pin
that the checker still refuses what it must. The declared table is
`ProjectDirectionPolicy.AllowedReferences` and the exempt set is
`ProjectDirectionPolicy.ConsumerProjects`; adding a reference means declaring it there in the same
change.

Application-layer replication addendum:
`KernelReplicationLayerBoundaryTests` (Tests project — the normative gates project targets net8.0
and cannot load the net48 layer) pins the stage-3 move: every moved kernel-replication type is
declared in the Application assembly, the Application assembly's reference set contains no Runtime,
GameAdapter, Plugin or game assembly, the Runtime still references the layer, and the three types
that deliberately stayed (`KernelWireMapper`, `KernelBatchItemProjection`, `KernelEnvelopeHandler`)
are named with their blocker — so moving one later is an edit to a recorded list, not silent drift.

Adapter-seam addendum:
`AdapterCapabilityPortShapeTests` freezes the Game Adapter seam. It asserts that `IGameAdapter`
declares no member of its own (a member added back onto the aggregate fails — the census reads
methods, properties and events alike), that each capability port declares exactly its pinned member
census and that no member name is shared by two ports, that the aggregate composes exactly those twelve
ports plus `IDisposable`, that the composition carries exactly sixteen members, and that every port is
registered EXACTLY ONCE from the one adapter singleton in `GameAdapterComposition` (counted, so an
unwired port and a duplicate whose last descriptor wins both fail; read as source, because the tests
load the adapter reflectively and cannot resolve its container). Its matcher is pinned by a synthetic
composition that declares
a method, a property and an event — so the check is known to flag the shape it exists for — and the
port list is the same one the aggregate names, not a second list. Two mutation controls were run in the
adapter cycle's landing (2026-09-21) and RE-RUN for the two ports added on 2026-09-22: declaring a member
back on the aggregate (with its implementation) and deleting one port registration each turn it red;
restoring them turns it green. The aggregate itself is not registered in the composition root:
nothing resolves the whole adapter, and the compile-time proof that one object implements the
composition is the class declaration (`docs/backlog/review/adapter-capability-ports.md`; the
composition moved from the plugin into the adapter project with
`docs/backlog/review/plugin-host-shell.md`, decision 213).

Game-assembly addendum:
`GameAssemblyReferenceGateTests` enforces the layout rule that only a declared game-binding project
compiles against the game's own code. It scans every project `CasualtiesUnknownOnline.slnx` lists —
through the one shared reader `ProjectDirectionPolicy.SolutionProjects`, so a new project is covered
the moment the solution names it — for a `Reference` whose Include is exactly `Assembly-CSharp`, and
allows it only for `CasualtiesUnknownOnline.GameAdapter` (the framework's only game-binding layer) and
`CasualtiesUnknownOnline.PinyinSearch` (the satellite mod's game-binding half, advanced-modification
policy §1.2); tests and tools stay unconstrained through the existing
`ProjectDirectionPolicy.ConsumerProjects`. It carries a census floor (13 projects), a positive half
(every declared binder is still in the solution and still binds — the gate cannot pass because the
reference disappeared or the project was renamed) and five synthetic matcher cases, including an
`Assembly-CSharp-firstpass` sample that must NOT be dragged in by a prefix match. The plugin project's
built DLL was the measurement that made the rule true: its assembly references carry no
`Assembly-CSharp`.

Scope of that scan, stated rather than implied: the gate reads each solution project's OWN project
file. A reference that arrives through an imported `Directory.Build.props` / `Directory.Build.targets`
or any `<Import>`ed props/targets file is outside it (the tree carries no such file today; if one ever
appears, the reader has to follow the MSBuild import closure).
