# Remote fentanyl injection bypass and remote medical panel desync

- Status: Todo
- Priority: Critical
- Category: Remote medical / cross-player medicine
- Source: User report (2026-09-06) — a guest used fentanyl on the host through the remote medical panel. The guest saw the whole syringe drain instantly with no native injection minigame; the host's medical panel did not show the fentanyl happiness change, heart rate displayed 0 while the waveform still animated, and the remote medical panel leaked the viewer's own bottom status icons and left the sleep button enabled.

## Problem

The current remote medical treatment path (`review/remote-medical-treatment-operations.md`)
supports injectable/IV medicines through `PlayerItemUseRequest` →
`RemoteMedicineCatalog` / `RemoteMedicineApplication`. That path is
host-authoritative and pure-snapshot based: it applies the curated per-ml
effects to a character snapshot and drains the item, but it does not reproduce
the native `SyringeMinigame` flow that local fentanyl injection uses
(`Item.cs:1035-1057`, `WaterContainerItem.Inject`).

The remote medical view itself is implemented as a native WoundView pointed at
a display-only body copy (`RemoteMedicalCoordinator`). The observed panel
problems suggest the display copy is not a faithful projection of the viewed
player's body: physiological/status fields (happiness, heart rate) and the
panel's non-body UI state (sleep button, bottom status icons) are not fully
redirected to the viewed player.

## Reported behavior

1. **Guest → host fentanyl use bypasses the injection minigame.**
   The acting guest uses fentanyl on the host; instead of the normal
   `SyringeMinigame` flow, the fentanyl container is drained completely in one
   instant action. User-visible expected behavior: the acting side should
   follow the normal injection interaction (or a designed remote equivalent)
   and only commit the amount the minigame actually delivers.

2. **Medical panel does not show the host's happiness change.**
   After fentanyl, the host's happiness should rise to 100 (fentanyl
   `onHealthUse` adds `420 * 0.1 * ml` opiate amount and drives opiate
   happiness). The guest viewing the host's medical panel sees no happiness
   change.

3. **Medical panel shows heart rate 0 while the waveform still animates.**
   The heart rate readout on the remote medical panel is 0 even though the
   ECG/waveform continues to animate. This is a display/snapshot consistency
   bug, not a host death state.

4. **Remote medical panel keeps the viewer's own status icon row.**
   The bottom status icons on the screen while viewing another player show the
   viewer's own stats, not the viewed player's stats.

5. **Sleep button remains enabled while viewing a remote player.**
   The native medical panel should not allow sleeping on another player's
   display body when the panel is in remote focus.

## Investigation scope / suspected gaps

- **Native injection minigame is missing from the cross-player medicine path.**
  Trace `Item.cs:1035-1057` (fentanyl local use starts `SyringeMinigame`),
  `SyringeMinigame`, `WaterContainerItem.Inject`, and compare with
  `RemoteMedicalOperationHandler` / `PlayerItemUseService` /
  `RemoteMedicineCatalog`. Determine whether the remote use should:
  - run the minigame on the acting client and only report the committed millilitres;
  - run the minigame on the target client and report back; or
  - use a designed remote-equivalent interaction.
  The current "host applies full curated dose immediately" is either a
  temporary shortcut or a missing native-interaction adaptation.

- **Happy/opiate consequences are not visible on the remote medical panel.**
  Trace `CharacterHealthMsg` capture (`CharacterDataCapture`), the host's
  1 Hz broadcast (`CharacterDataSync`), `RemoteMedicalCoordinator.ApplySnapshot`,
  and the WoundView field mapping. Verify whether `Happiness`, `OpiateAmount`,
  and the derived mood/happiness readout are mapped to the display body and
  refreshed after a cross-player use.

- **Heart rate zero while waveform animates.**
  Trace the heart rate field used by the WoundView text readout versus the
  field/signal used by the ECG waveform. Verify whether the display body's
  `heartRate` is mapped from `CharacterHealthMsg.HeartRate`, and whether the
  captured snapshot actually carries a non-zero host heart rate.

- **Bottom status icons belong to the viewer.**
  Inspect the native WoundView / PlayerCamera HUD status-icon path and
  determine which body/state it reads. The remote focus must redirect that
  readout to the display body (viewed player), not the viewer's own body.

- **Sleep button must be disabled in remote focus.**
  Check `RemoteMedicalPatches` / `PlayerCamera.WoundSpecialAction` / any nap
  entry used by the native medical panel. Sleep/nap must be suppressed while
  the remote medical focus is open, just like the read-only guarantee for
  other special WoundView actions.

## Acceptance criteria

- Guest using fentanyl on the host follows the normal injection interaction (or
  an explicit user-approved remote equivalent) instead of an instant full drain.
- After the injection completes, the guest viewing the host's medical panel sees
  the host's happiness rise to the expected post-fentanyl value.
- The remote medical panel shows a non-zero host heart rate consistent with the
  host's actual body, while keeping the waveform animated.
- While viewing another player: bottom status icons show the viewed player's
  stats, not the viewer's own.
- While viewing another player: the sleep button is disabled/blocked; no nap can
  start from the remote focus.
- All roles/directions are covered: guest→host, host→guest, and third-party
  views.
- Full build, tests, architecture/event/entity gates pass before review; latest
  DLLs deployed and artifact-verified before user acceptance.

## Non-goals / open questions

- Do not build a parallel CUO medical panel; continue reusing the native
  WoundView.
- The exact remote minigame interaction model (acting-client vs target-client vs
  no-minigame remote equivalent) is a functional design decision that needs user
  direction before implementation.
