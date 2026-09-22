# Patch bridge domain ports — the fluid domain leaves the aggregate

Date: 2026-09-22
Scope: `review/patch-bridge-domain-ports.md` stage 1. The seam the patch classes read — 87 of the 118
files in `src/CasualtiesUnknownOnline.GameAdapter/Patches/` — stops being the only place a domain's
patch surface can live: the fluid domain's eight members move into `IFluidPatchPort`, the aggregate
neither declares nor composes them, and a gate freezes the census of every seam (decision 214).

## What landed

- **`IFluidPatchPort` (new, 65 lines, 8 members)** carries the fluid domain's patch surface verbatim
  with its docs: `OnFluidFixedUpdate`, `OnFluidDrinkReported`, `TryRenderCustomLiquids`,
  `TryGetCustomLiquidColor`, `TryGetCustomWaterInfo`, `TryGetCustomLiquidName`,
  `TryDrinkCustomLiquid`, `ApplyLiquidTileBodyTouch`. The custom-liquid byte resolution sits here
  rather than in `IModContentPatchBridge` because every consumer is a `FluidManager` hook.
- **The aggregate is closed.** `IPatchBridge` neither declares nor inherits those members (554 → 531
  lines; declared census 104 → 96), so `PatchBridge.Impl?.<fluid member>` does not compile — the
  migration is enforced by the compiler rather than by convention, and the port cannot become the
  aggregate under a new name.
- **The port is served by the one bridge.** `GameAdapterBridge` declares `: IPatchBridge,
  IFluidPatchPort` (569 → 574 lines; the growth is the class doc recording the arrangement), and the
  static seam resolves the port by casting the bound bridge: `PatchBridge.Fluid`. The other seam
  objects (`ModContent`, `Carriage`, `SessionSurface`) still arrive through aggregate PROPERTIES,
  which the freeze forbids adding; a future install unit (catalog stage 3) is what would move the
  port's implementation off the bridge.
- **All three fluid patch classes read the port.** `FluidSimulationPatch` and `FluidDrinkPatch` keep
  the session flag on the aggregate (`IsSessionActive` belongs to the session domain and migrates with
  it) and resolve the port as part of their guard — `PatchBridge.Fluid is not { } fluid || ...` — so a
  seam that stopped serving the port falls back to the original path instead of swallowing it;
  `FluidCustomLiquidPatches` (six hooks) is port-only. `FluidWorldSync`'s doc records the route.
- **The gate.** `PatchBridgePortShapeGateTests` (19 cases, `NormativeGates.Tests`, Roslyn over source
  — the fast suite, no game assemblies) pins the aggregate's declared census and composition, every
  seam interface's census (the four earlier patch seams, `IModContentPatchBridge`, the new port), the
  implementation's composition, public surface (= exactly the seams it serves) and non-public members,
  the static seam's census, and the two "one name, one door" facts. The matcher has its own contract
  (five sample sources: a method, a property, an event, an indexer and multi-declarator fields, plus
  what it must ignore — mentions in comments or crefs, and constructors, destructors, operators and
  nested types) and the pins carry census floors.
- **`PatchBridgePortContractTests` (reflective, Integration)** asserts the same on the built adapter:
  the port exists with exactly its eight members, the aggregate no longer declares any of them, the
  bridge implements the port and declares every member, and the seam exposes both doors.

## Mechanism inventory

| Mechanism | Change | Evidence |
| --- | --- | --- |
| Fluid patch surface | Eight members moved from `IPatchBridge` into `IFluidPatchPort`, docs verbatim | `PatchBridgePortShapeGateTests.Seam_DeclaresExactlyItsPinnedMembers` (port row) |
| Frozen aggregate | The same eight removed from the aggregate's declaration; it composes only the four earlier patch seams | `...Aggregate_DeclaresExactlyTheFrozenCensus`, `...Aggregate_ComposesExactlyThePinnedSeams` |
| Migration completeness | No call site reaches a fluid member through the aggregate (it does not compile) | `...ThePort_IsNotReachableThroughTheAggregate`; `dotnet build` after the aggregate edit |
| Port resolution | `PatchBridge.Fluid => _bound as IFluidPatchPort`, served by `GameAdapterBridge` | `PatchBridgePortContractTests.Seam_ExposesTheAggregateAndThePort`, `...Bridge_ImplementsTheFluidPortAndDeclaresEveryMember` |
| Patch call sites | `FluidSimulationPatch` / `FluidDrinkPatch` (session flag stays on the aggregate) and `FluidCustomLiquidPatches` (six hooks) | `--filter "FullyQualifiedName~Patching"` 331/331 |
| One name, one door | No member name is declared by two seams or by a seam and the aggregate | `...NoMemberName_IsSharedByTwoSeams` |
| Class surface | The implementation's public surface is exactly the seams it serves; non-public members pinned | `...Bridge_PublicSurface_IsExactlyTheSeamsItServes`, `...Bridge_DeclaresExactlyThePinnedImplementationMembers` |
| Evidence anchor | Sync-coverage row F2's quote followed the moved call line | `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` (refused before the fix) |

## Verification

| Check | Result |
| --- | --- |
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings, 0 errors |
| `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <10 changed .cs>` | exit 0 |
| Normative gates (`CasualtiesUnknownOnline.NormativeGates.Tests`) | 119 total, 19 of them this gate's (the project read 100 before this change, per `architecture/plugin-host-shell-selfcheck.md`); 118 green in the frozen pre-commit tree — the single failure is `DeliveryChecklist_NoIncompleteRequiredBoxes`, whose boxes are filled after the change lands |
| Focused `--filter "FullyQualifiedName~Patching"` | 331/331 |
| Full suite `dotnet test CasualtiesUnknownOnline.slnx` (with build) | 3795/3795 = fast tier 2 097 (unchanged: this change added no untagged case here) + tagged tier 1 698; the four reflective port contract cases are the delta |
| Mutation control 1 — public member added to `GameAdapterBridge` alone | `Bridge_PublicSurface_IsExactlyTheSeamsItServes` red (18/19) |
| Mutation control 2 — member declared back on the aggregate (implemented on the class) + one port member re-declared on the aggregate | `Aggregate_DeclaresExactlyTheFrozenCensus`, `ThePort_IsNotReachableThroughTheAggregate` and `Bridge_PublicSurface_IsExactlyTheSeamsItServes` red (16/19) |
| Mutation restore | 19/19 green; `git diff` carries neither mutation |

## Limits

- Internal refactor: no real-machine, dual-client or in-game claim, and no user-visible behaviour
  change at all — the call reaches the same object, cast instead of inherited. Deployment is verified
  by artifact identity, not by playing.
- The port is a SHAPE, not yet a replacement mechanism: the seam still binds one object, so replacing
  one domain's behaviour needs the install unit (catalog stage 3), and the gate — not the type system
  — is what keeps `GameAdapterBridge` serving the port.
- The gate reads source; the compiled artifact is the contract test's job, and that test is
  Integration-tagged because it loads the adapter through `GameAssemblyHost`.
- `InternalsVisibleTo` is untouched: the port lives inside the adapter project, and decision 212's
  22-name census of Runtime internals still stands.
- An independent reviewer ran against the frozen tree before the commit (report kept outside the
  repository): 0 blocker, 1 major, 3 minor, 3 nits, all fixed in the same change — the major was stale
  facts this ticket carried into `docs/architecture/current.md` and decision 214 (the "94" member count
  and the "every patch class reads the seam" share), and one minor was the silent-degradation shape the
  fluid guards now avoid. The numbers above were re-measured after those fixes.
