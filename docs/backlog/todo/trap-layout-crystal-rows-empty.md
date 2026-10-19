# The trap layout never carries a crystal row

- Status: Todo — opened 2026-10-19 by the independent review of the `NetMsg.EntityEvent` drop lever
- Priority: Low-Medium
- Category: World sync / trap layout
- Source: the review of `docs/backlog/todo/member-report-bounds.md`'s entity-event acceptance lever (2026-10-19):
  the census recipe reads a crystal's effects, and the review checked the scanner that is supposed to do the
  same and found it returns nothing for every crystal
- Related: `done/trap-layout-snapshot-recovery.md` (the layout's repair path this surfaces in),
  `done/runtime-entity-spawn-backfill.md` (the runtime-creation channel that covers part of the same ground),
  `done/trap-layout-entry-snapshot-staleness.md`, `tools/acceptance/recipes/trap-drop-census.cs` (the recipe
  whose comment names the gap)

## The defect

`TrapEntityScan` has one row for the crystal family:

    new Row { Component = typeof(CrystalBehaviour), Kinds = CrystalKinds },

and `CrystalKinds` switches on the COMPONENT's runtime type name:

    private static EntityEventKind[] CrystalKinds(Component component) => component.GetType().Name switch
    {
        "CrystalFragile" => [EntityEventKind.CrystalFragileBroken],
        …
        _ => [],
    };

A `CrystalBehaviour` component's `GetType().Name` is always `"CrystalBehaviour"` — the effect that decides the
kind (`CrystalFragile`, `CrystalUnstable`, …) is a PLAIN class held in the behaviour's private `effects`
list (`reversing/Assembly-CSharp/Assembly-CSharp/CrystalEffect.cs`: `public abstract class CrystalEffect`;
`CrystalBehaviour.cs`: `private List<CrystalEffect> effects;`), never a component. So every crystal falls to
the `_ => []` arm and the scan yields **no crystal kind at all** — the layout the host builds and sends, and
the guest's own local enumeration used for the alignment, both omit the whole family. The class's own doc
comment says the opposite ("the public `CrystalBehaviour` base is scanned and the kind is derived from the
runtime type name"), and so does the comment on the row.

## The build-level fact this run found (2026-10-19, batch `20261019-b`)

The crystal hazard does not exist in the current build: the seven prefabs `WorldGeneration.PlaceCrystals`
loads (`BloodCrystal`, `SoothingCrystal`, `ReliefCrystal`, `TurbulentCrystal`, `OxygenCrystal`,
`EmissiveCrystal`, `DigestionCrystal`) carry no `CrystalBehaviour` component at all — a runtime prefab probe
answers `hasBehaviour=false` for every one (`probe-crystal-prefabs.json`), two scanned layers carry 28
crystal-prefab buildings with zero components (`probe-crystals-guest.json`, `probe-crystals-layer1.json`),
and nothing in the assembly adds one (`AddComponent<CrystalBehaviour>` appears nowhere). So the scanner's
crystal arm cannot fire on this build for a reason deeper than the runtime-type bug above — but that bug is
still real for the day a prefab carries the component, and this ticket's fix (read the effects list) is still
the right shape then. Evidence: `docs/evidence/acceptance/member-report-bounds-entity-event-drops-20261019-b.md`.

## What it affects

- The layout is the host-authoritative **repair** path: a guest whose world is missing a generated trap has it
  materialized, and a surplus local trap is destroyed (`TrapLayoutApplication` / `TrapLayoutAlign`). For
  crystals that repair has never run.
- Runtime-created crystals are not lost by this alone: the runtime-entity channel (`EntitySpawned` creation
  keys, the snapshot backfill) replicates creations and their deaths, so the gap is the GENERATED family's
  alignment and any guest whose generation diverged on a crystal.
- The layout also drives the guest's own scan for the alignment, so a fix has to change both sides at once.

## Why it was not fixed in the same cycle

Reading the effects would make crystals appear on BOTH sides of the alignment. The two sides roll a crystal's
effects during generation (`CrystalBehaviour.SetUpEffects` calls `Random.value` / `GetRandomEffect`); if the
host's and the guest's rolls ever differ for one crystal, the aligned kinds differ and the guest would destroy
its own crystal and materialize the host's — a visible spawn/destroy on every entry. Whether the effect rolls
are guaranteed identical is the question the fix must answer first (a world/determinism probe on both
clients), and that is its own cycle with its own acceptance rows, not a rider on an acceptance-lever change.

## What the fix owes

- `CrystalKinds` reads the effect list (the same private member `CrystalEffectAccess` and
  `tools/acceptance/recipes/trap-drop-census.cs` read) and yields one entry per effect kind, deduplicated —
  a crystal can roll several effects, and a duplicate kind must not make the alignment materialize twice.
- The host/guest effect-roll agreement answered with evidence (a two-client probe of one crystal's effects
  before enabling the rows), or a decision that the alignment must key crystals differently.
- The layout rows are position-keyed; confirm two crystals in one cell (if the generator can place them)
  cannot collide in the `(kind, cell)` table.
- Acceptance: a two-client entry with the rows enabled reads no spawn/destroy churn on ordinary crystals; a
  guest whose local crystal was destroyed by hand gets it materialized from the layout, and a surplus one is
  destroyed.
