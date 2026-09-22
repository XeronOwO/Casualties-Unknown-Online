# Patch bridge: per-domain ports with the aggregate frozen

- Status: Review
- Priority: Medium
- Category: Architecture / adapter seam
- Source: Loomi architecture review (2026-09-20), item 4
- Related: `review/adapter-capability-ports.md`

## Problem (evidence)

Corrected when stage 1 landed (2026-09-22), because this section is what the change copies forward: the
review's member count was approximate — `IPatchBridge.cs` measures **104** declared members in 554
lines, not 94 — and "every Harmony patch class" overstates the share: 87 of the 118 files in
`src/CasualtiesUnknownOnline.GameAdapter/Patches/` read `PatchBridge`, while 112 files under `src/`
declare `[HarmonyPatch]`-attributed classes (the "113 files carrying `[HarmonyPatch]`" this section
used to quote had already been recorded as stale in
`docs/evidence/selfchecks/tooling/game-update-contract-toolchain-selfcheck.md`).

`src/CasualtiesUnknownOnline.GameAdapter/IPatchBridge.cs` is 554 lines with 104 member declarations
(531 lines / 96 members after stage 1), and
`src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs` is 569 lines (574 after). The patch
classes read this one static seam — they never touch the global container — but the shape has four
costs:

- any domain change edits the central interface;
- a version difference cannot replace one domain's behaviour;
- test doubles grow with the whole surface;
- optional capability cannot be expressed, because everything is one contract.

## Goal

Per-domain ports — `IItemPatchPort`, `ICharacterPatchPort`, `IWorldPatchPort`, `IRunPatchPort`,
`IInteractionPatchPort`, `IModContentPatchPort` — installed by the patch unit that owns them
(the install unit arrives with `review/adapter-capability-catalog.md` stage 3).

**Rejected: the one-shot migration the review's item 4 implies.** Moving 113 patch classes at once
is a mechanical change whose regression surface is every patch in the tree, and no test in the suite
can prove it equivalent; the win (smaller merge conflicts, per-domain replacement) does not require
a big-bang. Do it a domain at a time instead.

**Rule this ticket establishes:** the aggregate interface is frozen. No new member may be added to
`IPatchBridge` / `GameAdapterBridge`; new patch work registers through a domain port.

## Stages

1. Port skeleton plus ONE domain migrated end to end as the template, with the aggregate still
   serving every other domain.
2. One domain per change afterwards, taken when that domain is being touched for its own reasons.

## Acceptance

- A shape gate fails on a new member added to `IPatchBridge` or `GameAdapterBridge`.
- The migrated domain's patch contracts and tests are green; unmigrated domains are untouched, and
  the change states which domains remain.
- The port surface is narrower than the aggregate for that domain — a port that carries the whole
  interface under a new name does not count as a split.

## Notes

Grammar note for the implementer: extension methods use the C# 14 `extension` block syntax, and the
patch classes must keep reporting only verified writes (a Prefix that swallows a write must not let
the same Postfix report it).

## What landed (2026-09-22)

Stage 1: the port skeleton plus ONE domain migrated end to end — the fluid domain.

- **`IFluidPatchPort`** (new, 65 lines, 8 members) holds the fluid domain's patch surface moved
  verbatim with its docs: `OnFluidFixedUpdate`, `OnFluidDrinkReported`, `TryRenderCustomLiquids`,
  `TryGetCustomLiquidColor`, `TryGetCustomWaterInfo`, `TryGetCustomLiquidName`,
  `TryDrinkCustomLiquid`, `ApplyLiquidTileBodyTouch`.
- **The aggregate is CLOSED, not merely frozen on paper.** `IPatchBridge` neither declares nor
  inherits those members (554 → 531 lines; its declared census 104 → 96 members, 96 + 8 = 104 before),
  so a call written against `PatchBridge.Impl` cannot reach a fluid member at all: the compiler — not
  a convention — is what makes the migration complete, and a port cannot degrade into the aggregate
  under a new name. `GameAdapterBridge` declares the port (`: IPatchBridge, IFluidPatchPort`; 569 →
  574 lines, the growth is the class doc recording the arrangement) and the static seam resolves it by
  casting the one bound bridge: `PatchBridge.Fluid`.
- **The whole domain's call sites moved.** `FluidSimulationPatch` and `FluidDrinkPatch` keep the
  session flag on the aggregate (`IsSessionActive` belongs to the session domain and migrates with it)
  and take the call through `PatchBridge.Fluid`; `FluidCustomLiquidPatches` (six hooks) is port-only.
  No call site in `src/` or `tests/` reaches a fluid member through the aggregate any more.
- **The gate.** `PatchBridgePortShapeGateTests` (19 cases, Roslyn over source, fast gate suite) pins
  the aggregate's declared census and its composition (the four earlier patch seams), every seam
  interface's census (those four, `IModContentPatchBridge`, the new port), the implementation's
  composition, its public surface (= exactly the seams it serves) and its non-public members, the
  static seam's whole census, and the two "one name, one door" facts. The matcher carries its own
  contract (five sample sources covering a method, a property, an event, an indexer and
  multi-declarator fields, plus the shapes it must ignore: mentions in comments or crefs, and
  constructors, destructors, operators and nested types) and the census pins carry floors.
  `PatchBridgePortContractTests` (reflective,
  Integration) asserts the same on the built adapter.
- **Mutation controls on the real tree** (2026-09-22): a public member added to `GameAdapterBridge`
  alone → `Bridge_PublicSurface_IsExactlyTheSeamsItServes` red (18/19); a member declared back on the
  aggregate (implemented on the class) plus one of the port's members re-declared on the aggregate →
  `Aggregate_DeclaresExactlyTheFrozenCensus`, `ThePort_IsNotReachableThroughTheAggregate` and
  `Bridge_PublicSurface_IsExactlyTheSeamsItServes` red (16/19); restoring the tree → 19/19 green and
  `git diff` free of both mutations.

### Domains still on the aggregate (stage 2 takes one at a time)

Item; character presentation and state; world generation; run and lifecycle; interaction (traps,
traders, speech, openables, pickups); enemy/creature; mod-content resolution. The install unit that
owns a port arrives with `review/adapter-capability-catalog.md` stage 3, so a port is served by the one
bridge (`GameAdapterBridge`) until then.

### Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx --verify-no-changes --include <10 changed .cs>`: exit 0.
- Normative gates: 119 total, 19 of them this gate's, 118 green in the frozen pre-commit tree — the
  single failure is `DeliveryChecklist_NoIncompleteRequiredBoxes`, unchecked until the cycle's boxes
  are filled. (The suite read 100 cases before this change, per the previous cycle's record in
  `docs/evidence/selfchecks/architecture/plugin-host-shell-selfcheck.md`.)
- Focused `--filter "FullyQualifiedName~Patching"`: 331/331.
- Full suite with build: 3795/3795 — the fast tier (2 097, unchanged: this change added no untagged
  case here) plus the tagged tier (1 698); the four reflective port contract cases are the delta.
- The sync-coverage anchor F2 followed the patch's line twice — once for the migration, once for the
  guard shape the review round changed — each time refused by
  `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine`
  (`docs/evidence/sync-coverage-evidence.json`) before the commit.

### Independent review round (2026-09-22)

An independent reviewer ran against the frozen tree (report:
`%TEMP%\cuo-review-patch-bridge-domain-ports.md`, kept; it describes the tree BEFORE the fixes below).
0 blocker, 1 major, 3 minor, 3 nits — all fixed in this same change:

- **major — stale facts carried into the review folder and copied forward.** This ticket's Problem
  section quoted the review's "94 member declarations" (measured: 104) and "113 files carrying
  `[HarmonyPatch]`" (measured: 118 files mention it, 112 declare it, 87 read `PatchBridge`), and both
  numbers had reached `docs/architecture/current.md` and decision 214. All three now carry the measured
  figures, and the Problem section states what was corrected and why.
- **minor — the matcher's sample count** was stated as six; it is five sample sources. Corrected here
  and in the selfcheck page.
- **minor — "a migrated member"** in the `IPatchBridge` doc implied an ongoing property while the gate
  pins a literal NAME census; the wording is now "a member that MOVED to a port".
- **minor — a silent-degradation shape** in `FluidSimulationPatch`: the port read sat in the call
  (`PatchBridge.Fluid?.OnFluidFixedUpdate()`) while the prefix returned `false` unconditionally, so a
  seam that stopped serving the port would freeze the host's fluid simulation with no log. Both fluid
  patch guards now resolve the port as part of their guard (`PatchBridge.Fluid is not { } fluid || ...`)
  and fall back to the original path instead of swallowing it; the state is unreachable today (one
  implementation, one bind) but it no longer degrades silently.
- **nits** — the unreproducible "100" and "3791" figures now name their source or their decomposition,
  and the checklist evidence suffix says what the gate actually pins.

### Limits

- Internal refactor: no real-machine, dual-client or in-game claim. Nothing here says the host or a
  guest behaves differently; there is no user-visible behaviour change at all (the call goes to the
  same object, cast rather than inherited), and deployment is verified by artifact identity, not by
  playing.
- The seam resolves a port by casting the one bound bridge, so this is a shape, not yet a replacement
  mechanism: a version adapter that replaced one domain's behaviour needs the install unit (catalog
  stage 3), and the gate — not the type system — is what keeps `GameAdapterBridge` serving the port.
- The gate reads source, so the compiled half is the contract test's job; that test is
  Integration-tagged because it loads the adapter through `GameAssemblyHost`.
- `InternalsVisibleTo` is untouched: the port lives inside the adapter project, and decision 212's
  22-name census of Runtime internals still stands.
