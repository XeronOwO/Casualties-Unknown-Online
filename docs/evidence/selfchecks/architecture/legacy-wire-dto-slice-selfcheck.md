# Self-check — the kernel <-> wire vocabulary moves into the Application layer

Ticket: `review/legacy-wire-dto-slice.md` (closes the move `review/application-layer-first-slice.md`
stage 3 left open). Decision: `docs/decisions/active.md` 215. Base tree: HEAD `26632345`.

## Mechanism x change x evidence

| Mechanism | Change | Evidence |
| --- | --- | --- |
| `KernelWireMapper` (526 lines) | Moved `Runtime/Session/Items/` -> `Application/Kernel/`; its nine enemy-combat call sites now bind to the layer-local pure mapper | `grep -rn "KernelEnemyCombatWireMapper\." src` -> 9 sites, all inside `Application/Kernel/KernelWireMapper.cs`; build 0 warnings / 0 errors |
| Legacy half of enemy combat | `EnemyCombatWireMapper` is now its three `*Msg` overloads only; `EnemyCombatKernelCodec` lost the two pure limb conversions | `grep -rn "EnemyCombatWireMapper\." src` -> 3 sites, all `EnemyCombatKernelSubmitter` (Runtime) |
| Three duplicated component conversions (two in `KernelWireMapper` — its helper pair and its inline `FromWireData` — and one pair in the Runtime interaction mapper) | Replaced by ONE `KernelComponentWireMapper`; all six call sites (3 write + 3 read across 3 files) go through it | field-set machine check against `git show HEAD:` versions: 7 fields, old == new; `grep -rn "new WireComponentField\|new WireComponentState" src` -> the only remaining construction is inside `KernelComponentWireMapper` |
| Limb vocabulary | NEW `KernelLimbWireMapper`: four conversions over the same 22 fields, with the enemy limb converted through the kernel's own limb shape instead of a second wire spelling | machine check: 4 x (22 old fields == 22 new fields), zero missing / zero extra |
| `PlayerInteractionWireMapper` | Moved and renamed to `Application/Kernel/KernelPlayerInteractionWireMapper.cs`; zero Runtime type references, and the kernel mapper was its only external caller | `grep -rn "Runtime\." <file>` at HEAD -> only its own namespace line; caller census at HEAD -> 8 production sites across 2 files (6 in `KernelWireMapper`, 2 limb helpers in `EnemyCombatWireMapper`) plus 3 test sites |
| `ItemSpawnWireMapper` (60 lines) | Moved | caller census -> only `KernelWireMapper` |
| `IKernelWireCodec` port + `KernelWireCodec` adapter | DELETED (a pure forwarder once the mapper is inside the layer); the layer calls its own mapper; the one Runtime-bound conversion is declared as `IKernelItemDataNormalizer` / `KernelItemDataNormalizer` | `grep -rn "IKernelWireCodec" src tests` -> 0 sites; registration is `AddSingleton<IKernelItemDataNormalizer, KernelItemDataNormalizer>()` |
| 30+ test call sites | `WireCheckpointAssembler.Split`/`Assemble` lost the codec parameter | build 0/0; suite green |
| Layer-boundary record | `KernelReplicationLayerBoundaryTests`: 14 types pinned inside the Application assembly, the two that stayed named with their reason | gates green; the theory rows grew 8 -> 14, which is the +6 case delta in the suite count |
| Evidence matrix + architecture docs | `sync-coverage-evidence.json` (3 rows) and `sync-coverage-matrix.md` (2 rows) re-pointed to the moved path; `architecture/current.md` and `architecture/guards.md` updated; the stale `todo/` references in decision 211 and the first-slice ticket re-pointed | `SyncCoverageGateTests` + `BacklogReferenceGateTests` green |

## Verification (measured)

- Build: 0 warnings, 0 errors. `dotnet format CasualtiesUnknownOnline.slnx` (write mode): exit 0.
  Raised by the adversarial review as N2 and recorded here as stated: write-mode `dotnet format` exits
  0 whether or not it rewrote anything, and the read-only `--verify-no-changes` check is never clean in
  this repository (12 report-only whitespace errors, all inside generated `obj/` files, none in this
  cycle's files). The claim therefore records that formatting ran, not that the tree was independently
  proven formatted.
- Normative gates: 139/139 with the checklist complete (138 pass + the checklist gate while the boxes
  were still open, which is the gate working as designed).
- Full suite with build: **3803 passed, 0 failed** (`CasualtiesUnknownOnline.Tests`, net48) plus the
  gate project above.
- Field-set machine check: the four limb conversions (4 x 22), the component conversions (7) and the
  nine enemy-combat conversions were compared against the HEAD bodies extracted with `git show`;
  zero missing and zero extra fields.
- No wire shape changed and the protocol version is untouched (a fact of the change, not a design
  input).

## What this does not prove

- No real dual-client run: the mapper move is a pure code relocation with no wire or behaviour change,
  so the runtime evidence is the suite plus the layer/assembly assertions, not a live session.
- `KernelBatchItemProjection` was NOT moved: the structural decision and its contract evidence are in
  the ticket. This cycle proves the vocabulary's move, not the projection redesign.
