# Comprehensive medical UI / medical-item minigame gap audit (fentanyl is a surface symptom)

- Status: Review
- Priority: Critical
- Category: Remote medical / cross-player medicine
- Source: User report (2026-09-06) — a guest used fentanyl on the host through the remote medical panel. The guest saw the whole syringe drain instantly with no native injection minigame; the host's medical panel did not show the fentanyl happiness change, heart rate displayed 0 while the waveform still animated, and the remote medical panel leaked the viewer's own bottom status icons and left the sleep button enabled.
- Landed: 2026-09-06 — native syringe minigame routing for cross-player injectable/IV medicines, partial-dose wire, remote WoundView display projection, ECG/Moodle redirection and sleep-button disable all implemented; full gates green; latest DLLs deployed and artifact-verified; awaiting final user dual-client acceptance.

## Acceptance findings (2026-09-06)

The user opened this review ticket for acceptance and found the following
issues. They are recorded in
`docs/backlog/todo/remote-medical-panel-acceptance-issues.md`; this ticket is
**not accepted** until they are resolved.

1. Guest injects fentanyl into the host; the guest sees the host's mood/happiness update only at 1 Hz, not the rapid post-fentanyl rise.
2. Host breathing stops; the guest sees the host's medical panel bottom icon show "通气不足" instead of the correct host state (other icons sync).
3. The guest sees the host's medical panel ECG that is still the guest's own, not the host's; after the host's heart stops, the ECG still beats normally.

## Problem

The current remote medical treatment path supports injectable/IV medicines through `PlayerItemUseRequest` → `RemoteMedicineCatalog` / `RemoteMedicineApplication`. That path was host-authoritative and pure-snapshot based: it applied the curated per-ml effects to a character snapshot and drained the item, but it did not reproduce the native `SyringeMinigame` flow that local fentanyl injection uses (`Item.cs:1035-1057`, `WaterContainerItem.Inject`).

The remote medical view itself is implemented as a native WoundView pointed at a display-only body copy (`RemoteMedicalCoordinator`). The display copy lacked the inactive-body derivations (opiate/antidepressant happiness, mindwipe, blood-pressure readout) and the native HUD pieces still read the viewer's own body.

## Landed changes

- **Native remote syringe minigame**: injectable/IV medicines dragged onto a remote limb now start the native `SyringeMinigame` against the display body. `RemoteMedicalOperationHandler` accumulates the actually delivered ml, keeps the syringe fill in sync via a temporary item condition projection, and on `MinigameBase.EndMinigame` sends `PlayerItemUseRequest` with the exact ml. The session is cancelled on remote-focus close; `StartMinigame` success is verified before the session is kept.
- **Partial-dose protocol**: `PlayerItemUseRequestMsg.DoseAmount` (proto field 4) carries the minigame-delivered ml; `0` keeps the previous full per-item dose. `RemoteMedicineCatalog` and `PlayerItemUseService` honor the explicit amount, capping by remaining liquid and drawing proportionally across liquid stacks. `ProtocolVersion.Current` bumped 10 → 11.
- **Remote display fidelity**: `RemoteMedicalCoordinator.ApplyDisplayDerived` explicitly projects heart rate, blood pressure readout, opiate happiness, antidepressant happiness and mindwipe onto the inactive display clone; stale mindwipe components are removed when no longer present.
- **Native HUD redirection**: while the remote WoundView is open, the ECG inside the WoundView reads the display body, `MoodleManager` reads the display body for the bottom status icons, and the nap button is visibly disabled (TakeANap remains blocked).
- **Selfcheck**: `docs/evidence/selfchecks/players/remote-medical-fidelity-and-syringe-minigame-selfcheck.md`.

## Acceptance criteria

- Guest using fentanyl on the host follows the normal injection interaction instead of an instant full drain.
- After the injection completes, the guest viewing the host's medical panel sees the host's happiness rise to the expected post-fentanyl value.
- The remote medical panel shows a non-zero host heart rate consistent with the host's actual body, while the ECG waveform follows the same display body.
- While viewing another player: bottom status icons show the viewed player's stats, not the viewer's own.
- While viewing another player: the sleep button is disabled/blocked; no nap can start from the remote focus.
- All roles/directions are covered: guest→host, host→guest, and third-party views.
- Full build, tests, architecture/event/entity gates pass before review; latest DLLs deployed and artifact-verified before user acceptance.

## Non-goals / open questions

- No parallel CUO medical panel; native WoundView remains the only medical UI surface.
- The exact remote minigame interaction model chosen here is "run the native minigame on the acting client and report only the delivered ml"; target-client and no-minigame alternatives are not implemented.
- Host-side anti-cheat validation of `DoseAmount` remains a future strict-validation item; accept-first arbitration semantics are unchanged.
