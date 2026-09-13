# Trap/entity action divergence hardening

- Status: Review (closed by the shared-action verdict follow-up cycle, 2026-09-13)
- Priority: Low-Medium
- Category: Sync / restore accounting
- Source: the S3 gap-3 cycle (`TrapActionOutcome` + `TrapActionVerdict`); the reviewer restated these
  as over-claims of the family that cycle fixed, with their reachability
- Related: `docs/backlog/todo/save-mid-run-consistent-cut.md` (the shared action verdict),
  `docs/architecture/save-archive-format.md` §6, `src/CasualtiesUnknownOnline.GameAdapter/World/TrapStateActions.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/CrystalStateActions.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/RestoredWorldFactReplay.cs`, decision 175

## The gap (as reported)

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

## What the cycle decided (per case, decompiled source as evidence)

The rule the four verdicts are now derived from — **the row's fact is what its TRIGGER PATCH
reported** — is recorded in `TrapActionOutcome`'s doc and as decision 175:

1. **`ApplyShower` → `NotApplicable` when `controller.shower == null`** (nothing written, no game call
   made). Red recorded before the change: `TrapActionClassificationTests` invoked the production body
   on a never-initialized `LifepodController` and got `NullReferenceException` from
   `LifepodController.ActivateShower()`, reached through `TrapStateActions.ApplyShower`.
2. **`ApplyHeat` → `NotApplicable` when `controller.heater == null`** — the SIBLING the ticket did not
   name, found by this cycle's family audit and closed in the same pass: `ToggleHeatState` writes
   `heater.desiredTemp` / `heater.enabled` on every step (`LifepodController.cs:25-26/33-34/39`), and
   `heater` is the other inspector member the controller's own lifecycle never touches
   (`Start` writes heatSprite/disinfectSprite/heatButton only, `LifepodController.cs:8-13`), so its
   absence is silent until an action calls. `LifepodHeatChanged` carries the POST-toggle state
   (`TrapLifepodButtonPatch.cs:25`) and is a durable projectable row, so its verdict reaches the
   account. Red recorded: the same test class, throw inside `LifepodController.ToggleHeatState()`
   through `TrapStateActions.ApplyHeat` (this host cannot execute that body — it fails on the Unity
   ECall inside it — which is the same evidence one step removed: the action called into the
   unguarded game write on a copy carrying no member).
3. **`ApplyBioTerminal` → `NotApplicable` when `terminal.GetComponent<BuildingEntity>()` is null**,
   decided BEFORE any write: the refusal writes nothing, so the beep and the 6 m door unlock do not
   run either — the row is refused as a whole (`TrapBioTerminalPatch.cs:21-32` reports the collider's
   enabled → disabled transition, i.e. the `Backgroundify` IS the row's fact).
4. **`ApplyCrystalShy` mirrors the `activated` latch, and the verdict answers THAT.** The latch comes
   first because the trigger patch observes exactly that rise
   (`TrapCrystalPatch.ShyTouchedPrefix/Postfix` → `ReportCrystal`) and the game writes it after the
   scan whatever the scan found (`CrystalShy.cs:31`) — the replay used to skip it, which left this
   copy ARMED (its own player's next touch swapped again and reported a second row) and answered
   `Applied` for a state it had never written. The two evidence questions the ticket asked, answered:
   - **What the row's position means after a swap**: it is the shy crystal's POST-swap position. The
     report patch reads `crystal.crystal.transform.position` in its POSTFIX
     (`TrapCrystalPatch.ShyTouchedPostfix` → `ReportCrystal`), i.e. after `Touched` swapped — so the
     row names the partner's pre-swap position (unless the scan matched the crystal's own collider,
     in which case nothing moved and the position is unchanged).
   - **Whether a late-joiner's replay re-swaps the right pair**: a replay starts the same scan from
     that position, which on a freshly generated world is where the PARTNER stands, so the pairing is
     decided by the same unspecified `Physics2D.OverlapCircleAll` order the game's own scan carries
     (a self-match is a no-op swap on both sides). The mirror is faithful; whether the pair converges
     can only be settled by the dual-client pass. The VERDICT does not depend on the scan: the row's
     fact is the latch rise its patch reports, and a scan that finds no partner is the same no-op the
     GAME's own `Touched` produces on that copy — so the ticket's original "refuse when the scan finds
     nothing" reading was DROPPED as inconsistent with the patch-report rule (it would have reported
     a fully mirrored row as refused, i.e. a false "restore incomplete").

**Three more over-claims of the same family, found by the cycle's adversarial review and closed in
the same pass** — all three are rows whose trigger patch reports an `activated` latch the replay never
wrote:

- **`ApplyCrystalMetamorphic`** wrote only the flash and the laugh while its row is a one-shot
  consumption that reaches the account (`EntityEventProfiles` → `WorldEntityKernelProjection.BuildFacts`
  → `EntityEventSync.OnTrapStateProjected`). The crystal dies inside its own `Touched`
  (`CrystalMetamorphic.cs:26`), the kind is NOT in `TrapDamageProfiles`, so no health row rides the
  trigger batch — the old comment's claim that the death "rides `BuildingEntityDamaged`" was false. It
  now mirrors BOTH marks the row needs: the effect's `activated` latch and the death (health = 0 as a
  REMOTE death, so no second drop roll on this side). The death ALONE was not enough — and that is why
  the latch is written FIRST: `health = 0` only makes the entity's own Update destroy it on its next
  run (`BuildingEntity.cs:56`) and `RemoteEntityDeath` suppresses only the building's drop roll, so
  during that frame the peer's own player could still touch the crystal, run the game's own `Touched`
  (whose only guard IS the latch, `CrystalMetamorphic.cs:18-21`) and roll a second set of 1..4 drops.
  The `ApplyCrystalFragile` sibling is not the same shape: that kind has no latch and rolls no drops of
  its own, so its window costs a duplicate sound at worst.
- **`ApplyCrystalEMP`** wrote the white flash but never the latch its patch reports
  (`TrapCrystalPatch.EmpTryEMPPostfix`), and the crystal's own Update returns unless the latch is set
  (`CrystalEMP.cs:54-58`) — so the replicated crystal stayed permanently WHITE and still armed (this
  side's own player could touch it and drain their own batteries). It now mirrors the latch first.
- Both new latch sites plus the mimic's go through ONE extracted lookup/latch rule
  (`CrystalEffectAccess`; `CrystalMimicAccess` and `CrystalUnstableAccess` now delegate to it), so the
  untyped `effects` scan and the typed latch read/write exist once, and `GameFieldContractTests` locks
  `CrystalShy.activated` and `CrystalEMP.activated` like the mimic's.

The three replays' `observerlaugh` / `crystalenemylaugh` / `crystalemp` calls also had the two
`Sound.Play` flags INVERTED against the game's own calls (`true, false` = 2D + no pitch shift,
`Sound.cs:52`; the other five crystal calls already matched) and now match them.


**Two siblings considered and deliberately left** (same audit, opposite answer, with the rule stated
in their code docs): `ApplyScrapEater` and `ApplyMedStation` keep `Applied` — their patches report the
entity's own latch/value (`TrapScrapEaterPatch.cs:21-27` reports every successful feed's gauge,
`TrapMedStationPatch.cs:17-22` reports the `didHeal` rise), and those writes are unconditional, so the
row IS in the world even when a guarded follow-on write is skipped. A null `build` there would already
be throwing in the game's own `ScrapEaterScript.Update` (`ScrapEaterScript.cs:16`) on that copy.

## The containment question, answered

**Does a throwing row abort the whole live-world half?** It did, and it no longer does: the
world-entity write is now contained in its own step, so a throw there reports THAT half as not fully
written (with the throw as its reason) and releases only that half's handover, while the halves that
had already landed keep their accounting and their commit. The answer is pinned at the Runtime seam by
`RestoredWorldFactReplayTests.ApplyIfPending_WhenAWorldEntityRowThrows_DoesNotBlameTheHalvesThatLanded`
(red on the pre-fix code: `Assert.DoesNotContain` matched "the live-world write threw" against the
world-fact half, which had in fact landed and committed). The pre-existing test
`ApplyIfPending_WhenTheWriteThrows_ReportsIncompleteAndReleasesBothHandovers` still pins the other
direction: a throw in the world-fact steps themselves fails both halves, because the entity write
never ran.

**What the containment does NOT do.** A throwing row still costs the rows BEHIND it in the same
half, and the two appliers that have not run — the throw leaves no count, so the half's report can only
say "not fully written". One level deeper (a per-row catch inside the adapter's row loops, which the
adversarial review showed can keep BOTH the exception in the log and an exact refused-row count) is
recorded as `todo/restored-entity-row-containment.md`: it is a different mechanism with its own
evidence problem (the appliers are game-typed and carry read-only review only), not a footnote of this
ticket.

## Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| `LifepodController.shower` (inspector member, untouched by the game's own lifecycle) | `ApplyShower` refuses before the game call | `LifepodController.cs:8-13`/`:45-49`; red→green in `TrapActionClassificationTests.ShowerAction_OnACopyWithNoShower_AnswersNotApplicable` (NRE at `ActivateShower` before the guard) |
| `LifepodController.heater` (same shape, found by the family audit) | `ApplyHeat` refuses before the toggle loop | `LifepodController.cs:25-26/33-34/39`; red→green in `TrapActionClassificationTests.HeatAction_OnACopyWithNoHeater_AnswersNotApplicable` |
| `BioTerminalScript.building` (the unlock IS the `Backgroundify`) | `ApplyBioTerminal` refuses, writing nothing | `BioTerminalScript.cs:11`/`:33-43`, `TrapBioTerminalPatch.cs:21-32`; read-only review (the body starts with `GetComponent`, which this host cannot call) |
| `CrystalShy` latch (its patch's fact) | latch mirrored first; the verdict answers the latch, not the scan | `CrystalShy.cs:17-30`/`:31`, `TrapCrystalPatch.ShyTouchedPrefix/Postfix`; read-only review (`Component.transform` and `Physics2D` are members this host cannot bind) |
| `CrystalEMP.activated` (its patch's latch) | latch mirrored, so the crystal's own Update darkens it and the copy is consumed | `CrystalEMP.cs:20`/`:54-58`, `TrapCrystalPatch.EmpTryEMPPostfix`; contract row; read-only review (same host limit) |
| `CrystalMetamorphic` latch + death (its patch's fact) | latch mirrored FIRST, then health = 0 as a REMOTE death | `CrystalMetamorphic.cs:18-21`/`:26-33`, `TrapDamageProfiles` (the kind is absent, so no health row rides the batch); contract row; read-only review (same host limit) |
| the crystal effect lookup (mimic, unstable, and the three new sites) | ONE `CrystalEffectAccess`: find by runtime type name + the `activated` latch rule | `CrystalMimicAccess`/`CrystalUnstableAccess` delegate to it; contract rows; `TrapActionClassificationTests` (machine: the latch rule on a never-initialized crystal, and the mimic action's refusal) |
| `RestoredWorldFactReplay`'s throw scope | the world-entity write is its own contained step | red→green in `ApplyIfPending_WhenAWorldEntityRowThrows_DoesNotBlameTheHalvesThatLanded`; the opposite direction still pinned by `ApplyIfPending_WhenTheWriteThrows_ReportsIncompleteAndReleasesBothHandovers` |

## Residuals (recorded, not closed)

- **The rows behind a throwing row** are still lost (see the containment section) —
  `todo/restored-entity-row-containment.md`.
- **Shy-crystal pairing is order-dependent on the replay side.** The row's position is post-swap (see
  above), so a replay scans from the partner's position and depends on the same unspecified
  `Physics2D.OverlapCircleAll` order the game's own effect depends on. Faithful mirror, unverified at
  runtime: a dual-client pass that trips a shy crystal and watches both worlds' crystal positions
  would settle whether the pair converges. The `NotApplicable` path no longer depends on the scan (it
  fires only when the crystal carries no shy effect at all, like the mimic's).
- **`TrapEntityScan.CrystalKinds` is dead for crystals** (found by the review, pre-existing, outside
  this diff): the row is registered for `CrystalBehaviour` (`TrapEntityScan.cs:46`) but the switch
  reads `component.GetType().Name` (`:128`), which for every crystal is `CrystalBehaviour` — so the
  `_ => []` arm always wins and crystal kinds never produce a trap-layout row. Nothing is broken
  today (the crystal facts travel the kernel consumption channel, which is what the restore reads);
  the per-crystal-type arms are unreachable code whose purpose needs a decision.
- **`PlayerCamera.main` dereferences** in the presentation-only actions (`ApplyCoil`, `ApplyJumpPad`,
  `ApplyCrystalElectric`, `ApplyCrystalEMP`, `ApplyBearTrapClamped`, `TrapVisualReplay`'s explosion
  visuals) stay unguarded. The game assigns it in `PlayerCamera.Awake` (`PlayerCamera.cs:704-706`) so
  it exists on every path these replays run on, but that is an ARGUMENT, not evidence — the codebase
  itself guards it elsewhere (`TrapStateActions.ApplyMedStation`, `ExplosionBodyEffect`,
  `LifePodPresentation`, `WorldTimeSync`). If it is ever null there, the half-scoped containment above
  bounds the damage to the entity half.
- **Pre-existing, recorded by the same review, not touched here**: `WorldEntityState` keys
  consumptions by position, so two one-shot kinds at one position overwrite each other (a crystal can
  carry up to four effects, `CrystalBehaviour.cs:91-102`); `ApplyBearTrapClamped`'s destroyed teeth are
  not restored when a restore replays clamp → release (`TrapStateActions.cs`); with an audit but no
  `BeginRestore`, `WorldRestoreAudit` closes on the first contribution (production always opens the
  account, `WorldRestoreApplier`).
- **Historical audit refs are stale** (`docs/history/audits/runtime-supply-refresh-audit.md:71` cites
  `TrapStateActions.cs:379-384` for the metamorphic crystal, which moved to
  `CrystalStateActions.cs` when that family was extracted). Point-in-time document, pre-existing, not
  touched here.

## Not in scope (unchanged)

- The live relay paths: a wrong verdict there only mislabels a log line (the host's `ApplyState` and
  the guest's live `Replay` return value feed no account).
- Re-classifying the destructive families (mine/turret/unstable-crystal): they own their consumption
  checks inline by design and were audited in the same cycle.

## Acceptance

- Each action's verdict is decided per case with the decompiled source as evidence, and the per-row
  containment question (does a throwing row abort the whole live-world half?) is answered with a test
  at the Runtime seam (`RestoredWorldFactReplayTests` has the throw path already). **Done.**
- Machine evidence: the actions' game-typed bodies cannot be instantiated in the test host, so the
  classification half is read-only reviewed and the containment half is pinned at the Runtime seam —
  the same split the rest of this domain carries. **Done, and exceeded for three cases**:
  `TrapActionClassificationTests` drives the production `ApplyShower`/`ApplyHeat` bodies and the
  extracted `CrystalEffectAccess.TryActivate` rule on never-initialized game components (real
  red→green for the two lifepod refusals; the latch rule is a new-feature behaviour test). The two
  crystal ACTION bodies are NOT host-drivable and the reason is recorded: their bodies read
  `Component.transform` / `CrystalBehaviour.build`, Unity members the runtime binds for the WHOLE
  method (observed: `SecurityException: ECall methods must be packaged into a system module` raised
  from the action frame, before the early return could run).
- **Two adversarial review rounds** (independent fresh contexts). Round one found the three crystal
  over-claims above (each verified here against the decompiled sources before acting), the containment
  claim's overstatement, and three false statements in the cycle's own comments/records. Round two
  found the metamorphic LATCH gap (the death alone left a one-frame window in which the peer's own
  player could still re-run the game's `Touched` and roll a second drop set — closed here by writing
  the latch first), the shy verdict/rule contradiction (closed by answering the latch), duplicate
  contract rows, a dead accessor, and the remaining wording errors; its BLOCKER was the
  delivery-checklist box, which had been deliberately reset and re-checked line by line before the
  review landed.
