# Architecture Guards

Active enforcement rules for the typed deterministic kernel. They are implemented as
tools checks inside `tools/check-architecture.ps1`; they were added phase by phase and
are now a standing guard suite. The list is retained here as the authoritative set of
kernel-shaped invariants.

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

## Suggested tooling shape

- A project reference / namespace analyzer run during build.
- A reflection or source-scan pass in `tools/check-architecture.ps1` or a successor.
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
| E | Full 10-item guard list; strict no-legacy failure mode. |

## Relationship to existing gates

The existing repository gates (600-line classes, state bool limits, one top-level type
per file, event-replay matrix, entity-event dispatch) remain in force. The guards above
are additions for the new deep architecture, not replacements.

## Landed guard automation

Guard 1 (GameState isolation) is automated by `tools/check-gamestate-isolation.ps1`
and runs inside `tools/check-architecture.ps1`. It rejects CUO project references,
Unity/BepInEx/Steam/network packages, raw assembly references, forbidden source
namespaces, ambient random/wall-clock usage, and (Phase B) Protocol DTO/protobuf
tokens in `src/CasualtiesUnknownOnline.GameState`.

Phase B addendum: `tools/check-item-authority.ps1` runs inside the same architecture
gate and rejects direct mutations of `WorldItemTable`/transfer-table state outside
the item projection classes, so old tables stay rebuildable projections.

Phase E addendum: `tools/check-no-legacy.ps1` runs inside the same architecture gate
and rejects dual-architecture type declarations (`Shadow`/`Legacy`/`Compat`/`Dual`)
plus known removed direct-result/legacy wire markers in `src/`.

Phase E addendum: `tools/check-command-authority.ps1` runs inside the same architecture
gate and requires every `GameCommand` subclass in the GameState kernel to carry an
`AuthorityKind`/authority policy.

Phase E addendum: `tools/check-kernel-shape.ps1` runs inside the same architecture gate
and rejects string-keyed dictionaries or `Hashtable` state in the GameState kernel.
