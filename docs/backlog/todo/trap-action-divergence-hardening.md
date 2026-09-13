# Trap/entity action divergence hardening

- Status: Todo (split out of `todo/save-mid-run-consistent-cut.md` by the shared action verdict cycle,
  2026-09-13 — the adversarial pass of that cycle found them while auditing the same family)
- Priority: Low-Medium
- Category: Sync / restore accounting
- Source: the S3 gap-3 cycle (`TrapActionOutcome` + `TrapActionVerdict`); the reviewer restated these
  as over-claims of the family that cycle fixed, with their reachability
- Related: `docs/backlog/todo/save-mid-run-consistent-cut.md` (the shared action verdict),
  `docs/architecture/save-archive-format.md` §6, `src/CasualtiesUnknownOnline.GameAdapter/World/TrapStateActions.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/CrystalStateActions.cs`

## The gap

The shared action library now answers a tri-state verdict (`TrapActionOutcome`: `Applied` /
`AlreadyInState` / `NotApplicable`) and the restore's live-write account counts a row as reached only
when the action applied it or the local copy already carried the state. Three actions still answer
`Applied` (or throw) in a case where the fact is NOT in the world, so the account can still be told a
row was restored that exists nowhere:

1. **`TrapStateActions.ApplyShower` throws on a controller without a shower.**
   `controller.shower != null && controller.shower.activated` is checked, but the else-path calls
   `LifepodController.ActivateShower`, which dereferences `this.shower` unconditionally
   (`LifepodController.cs:45-47`). A null `shower` is a serialized lifepod prefab member, so no
   reachable case is demonstrated — but the BLAST RADIUS is what makes this worth its own ticket: on
   the restore path the throw reaches `RestoredWorldFactReplay`'s catch, which marks the whole
   live-world write incomplete and releases EVERY handover (the world-fact marker, the native
   handover, the world-entity facts and the item reconcile), so one bad row's exception is reported as
   the whole restore failing rather than as that row being refused. Decide both halves: the action
   answers `NotApplicable` instead of throwing, and/or the per-row loop contains a throwing row
   (one refused row, the rest applied).
2. **`TrapStateActions.ApplyBioTerminal` answers `Applied` with no `BuildingEntity`.** The unlock IS
   `building.Backgroundify()`, so when `terminal.GetComponent<BuildingEntity>()` returns null the
   audible part plays while the state the row names is not written. The component is expected
   (`BioTerminalScript.cs:11`/:33), so reachability is unproven; the honest verdict is `NotApplicable`.
3. **`CrystalStateActions.ApplyCrystalShy` answers `Applied` when its scan finds no neighbour.** No
   swap happened, so the row names a state that is not in the world. This one is a DIFFERENT defect
   shape from the family the tri-state fixed (a `true` that did nothing, not a conflated `false`) and
   it needs an evidence question answered before it is patched: what the row's position means after a
   swap (the trigger's position is captured around the swap) and whether a late-joiner's replay
   re-swaps the right pair. The game's own `CrystalShy.cs:17-30` scan is identical and can match the
   crystal's own collider, so the mirror is faithful — the over-claim is the verdict, not the scan.

## Not in scope

- The live relay paths: a wrong verdict there only mislabels a log line (the host's `ApplyState` and
  the guest's live `Replay` return value feed no account).
- Re-classifying the destructive families (mine/turret/unstable-crystal): they own their consumption
  checks inline by design and were audited in the same cycle.

## Acceptance

- Each action's verdict is decided per case with the decompiled source as evidence, and the per-row
  containment question (does a throwing row abort the whole live-world half?) is answered with a test
  at the Runtime seam (`RestoredWorldFactReplayTests` has the throw path already).
- Machine evidence: the actions' game-typed bodies cannot be instantiated in the test host, so the
  classification half is read-only reviewed and the containment half is pinned at the Runtime seam —
  the same split the rest of this domain carries.
