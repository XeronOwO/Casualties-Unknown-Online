# Architecture watchlist

- Status: Watchlist
- Category: Architecture

Files at/near the 600-line hard gate (`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`)
must be split — as a real responsibility split, never by shrinking formatting — before the next
change lands in them.

## At the limit (no headroom)

- None right now: the medical family's two 600-line services were split on 2026-09-19 (see
  "Split since the last revision").

## Near the limit (watch)

- `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs` (+ its
  `EnemySyncCoordinator.RuntimeSpawns.cs` partial, 584 aggregate after the 2026-09-18 split) — the
  host capture (id allocation in the deterministic `EnemySpawnArbitration` order, the bind-time
  spawn anchor, the per-frame state capture) and the guest binding half (the spawn-anchor pairing,
  the runtime-spawn materialization, the frozen-copy lifecycle). What is left to extract next is
  the HOST CAPTURE half (`CaptureHostEnemies` / `EnsureMapping` / `Bind` / `Capture` ≈ 110 lines);
  the shared entity↔id table is what currently ties it to the guest binding half.
- `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` (~583) — the pump order is the seam
  contract, so any further per-frame step should go into a domain, not into `Update`.
- `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` (~582) — the run-lifecycle phase
  machine plus the body-state publish path. It grew by one line in the S2 in-game gap fix (the
  WorldJoin follow cancels a queued local character restore); a split is due before anything else
  lands in the phase machine or the publish path.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs` (~572) — the character
  domain's session-scoped coordinator (the 1 Hz report, the local clone fact table, and the wiring of
  an arriving restore into the queue). The S2 in-game gap fix moved its local restore queue to the
  role-neutral `QueueLocalRestore`/`CancelLocalRestore` pair and then sent the queue STATE itself to
  the Runtime (`Runtime/Session/CharacterData/` `LocalCharacterRestoreQueue`, pure and unit-tested)
  once the file crossed the gate at 606 lines. What is left to extract next is the APPLY half
  (`TryApplyCharacterRestore` / `ApplyRestoredStatsAndWipe` / `ApplyRestoredItems` / `RestoreWearable`
  ≈ 190 lines, with `PlayerInteractionApply` as the second caller of the last one).
- `src/CasualtiesUnknownOnline.Runtime/Session/Commands/CommandConsoleService.cs` (~510) — command
  groups register as their own owners (`HostAdminCommands`, `WorldSaveCommands`); a new command
  family must not grow this class.

## Split since the last revision

- `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/` — the demanded split happened
  (2026-09-19, with the interaction-gate relocation): the medical family's shared reservation
  bookkeeping (`_reservedItems`, `_reservedTargetLimbs` and the cross-service "operator busy"
  lambda chain that each service used to answer for the other two) moved into
  `MedicalOperationClaims`, one owner with one claim rule. `MedicalOperationSessionService`
  600 → 595, `ShrapnelOperationSessionService` 600 → 553, `OtherMedicalOperationSessionService`
  524 → 520, and all three have headroom for the change that needed it. The shrapnel family's
  start validation also left the service as the pure `ShrapnelStartValidator` (the pattern the
  stage-3 family already used), so the service holds the session lifecycle and the validator
  holds the rules.

- `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.cs` — the demanded split
  happened (2026-09-18, with the N1 enemy binding recovery): the coordinator sat at exactly the cap
  (388 + 212 = 600) and the change pushed it to 626, so the presentation-apply half moved into
  `EnemyPresentationApplier` — writing one authoritative state onto its bound copy (transform,
  reconciled health, stun pose, spider leg IK, crystal wind-up telegraph) and the in-flight
  local-damage table it reconciles against. The coordinator now owns identity/binding/lifecycle
  only: 626 → 584.
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs` — the demanded APPLY split
  happened (2026-09-11, with S3.4b): the restore's write half moved into `CharacterRestoreApplier`
  (stats + wipe, items, the native character fields) and the worn-item write into `WearableRestorer`,
  which both the restore and `PlayerInteractionApply` call. The coordinator now owns only WHEN a
  restore runs — the queue, the position gate, the two-frame rhythm, the 1 Hz report and the capture.
  596 → 508 lines.
- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs` — the demanded split
  happened (2026-09-11): the restore half (`TryContinue` + the salvage/summary assembly + the local
  character's apply contract) moved into `WorldRestoreApplier`, and the service now only ADOPTS the
  identity a restore produced as its write target. 513 lines, no longer near the gate; what remains
  is the trigger lifecycle (armed request, deferral window, report) over `WorldCutWriter`.
