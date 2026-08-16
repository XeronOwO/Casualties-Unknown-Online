# Start-gate wait-info & layer-title alert selfcheck (#87)

Cycle: start-gate wait presentation. ProtocolVersion unchanged. 903 tests green
(L0 reflection + patch contract + static asset/source evidence; no manual
acceptance — development-period rule).

## Context

`docs/backlog.md` #87 has two coupled presentation defects:

1. The multiplayer wait info was a centered IMGUI label, while the game's own
   loading-screen info slot is anchored at the bottom-right corner.
2. The layer-title popup is built immediately after the loading screen hides
   (`WorldGeneration.FinishWorldGeneration`), so on the host it starts its 6 s
   unscaled lifetime BEFORE the world-entry edge arms the start gate — it can
   play out invisibly during the gate wait.

## Mechanism inventory (before the change)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The game's loading info slot is bottom-right | `level1` scene: `LoadingImage/Text (TMP)` RectTransform path id 1195 — anchorMin/Max (1,0), anchoredPosition (-106.6, 25), sizeDelta (202, 50). `WorldGeneration.loadingText` field: `WorldGeneration.cs:4171` |
| 2 | CUO wait text was centered over the whole screen | `Plugin.DrawWaitingOverlay` old `Rect(0, Screen.height*0.4, Screen.width, 60)` |
| 3 | Layer-title popup fires at generation end | `WorldGeneration.cs:3637` (`loadingObject.SetActive(false)`) then `:3640-3659` (`PlayerCamera.main.DoAlert(text, true)` / `DoAlertDelayed`) |
| 4 | The host's start gate arms one frame LATER | `RunCoordinator.UpdateSceneState` detects the world-entry edge (`RunCoordinator.cs`), then `WorldService.StartStartGate`; the alert fires in the coroutine step before that edge |
| 5 | Alert lifetime is unscaled | `PlayerCamera.cs:3050-3058` (`alertTime -= Time.unscaledDeltaTime`, 6 s span) — pausing via `timeScale=0` does not freeze the popup |
| 6 | Other DoAlert call sites | `PlayerCamera.cs:72` (hand-switch warning), `:747` (mood refusal), `:1611` (pickup-wearable hint), `WorldGeneration.cs:3640/3665` (layer title) — all ride the same `DoAlert` entry |

## Change

### Bottom-right wait info

`Plugin.DrawWaitingOverlay` now pins a translucent black panel to the
bottom-right corner (`margin 24`, `height 64`, `maxWidth 520`, right-aligned
white text). It stays over the live frozen world — no full-screen blackout
regression.

### DoAlert deferral through the start-gate window

- `PlayerCameraDoAlertPatch` (thin Harmony prefix) asks the bridge
  `TryDeferStartGateAlert(text, important)`; `true` skips the original.
- `RunCoordinator.IsStartGateAlertWindow` is the pure window:
  - guest: follow phases `Generating` + `WaitingReady`;
  - host: a local `_hostStartGateAlertPending` latch, set at the
    `GenerateWorld` boundary and cleared by `MarkPlayingForHost` (gate release /
    no-guest immediate start), world exit, or session end.
- `StartGateAlertQueue` stores suppressed popups; `StartGateCoordinator`
  queues in the window and replays in capture order once `IsPlaying` is true.
  If the window closes without playing (world left / session ended), the queue
  is cleared — a stale layer title can never leak into the next lobby.
- The delayed description coroutine is covered automatically: its
  `WaitForSecondsRealtime` finally calls the same `DoAlert` entry
  (`PlayerCamera.cs:2719-2723`), which the prefix sees and defers when the
  window is still open.

### New compile reference

`UnityEngine.TextRenderingModule.dll` is now a Plugin compile reference
(`TextAnchor` for the overlay alignment). On-demand policy: one DLL added,
`references/README.md` updated. The module is game-owned and already present in
the game install; CUO never deploys it.

## Whole-family audit

| Family member | Disposition |
|---|---|
| Layer-title `DoAlert(text, true)` | deferred during the window, replayed after release |
| `DoAlertDelayed` layer description | same entry point — deferred when its 6 s realtime delay lands inside the window |
| Hand-switch warning / mood refusal / pickup-wearable hint | cannot originate during the frozen wait (no input, timeScale 0), but would defer correctly if one did; unchanged outside the window |
| LifePod landing sound/shake | unchanged — already has its own `LifePodPresentation` defer/replay path |
| Loading-screen jitter + keep-visible | unchanged — the bottom-right panel draws over the kept loading screen |
| Session end / world leave | queue cleared, host latch cleared — no cross-run residue |

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Wait-info placement | centered label → bottom-right panel | `Plugin.DrawWaitingOverlay` code; asset evidence #1 above |
| Host alert window before gate arm | local latch set at `OnWorldGenerate`, cleared on playing/exit/session end | `RunCoordinator.cs` `IsStartGateAlertWindow`, `MarkPlayingForHost`, `UpdateSceneState`, `OnSessionEnded` |
| Guest alert window | follow phases `Generating`/`WaitingReady` | `RunCoordinator.IsStartGateAlertWindow` |
| Queue ordering + drain | FIFO `StartGateAlertQueue` | `StartGateAlertPatchTests.Queue_PreservesCaptureOrder_AndTakeAllDrains` |
| Queue clear | `Clear` drops pending | `StartGateAlertPatchTests.Queue_ClearDropsEveryDeferredAlert` |
| Patch shape | `PlayerCamera.DoAlert(string,bool)` prefix returns skip verdict | `StartGateAlertPatchTests.Prefix_SkipsOnlyWhenTheBridgeDefers_AndRunsWithoutASession`; `PatchContractTests.EveryContract_ResolvesWithExactSignature` |
| Patch inventory completeness | new attributed patch class auto-enters `BuildContracts` | `PatchContractTests.Contracts_CoverEveryAttributedPatchClass_PlusTheDynamicOnes` |
| No-session/solo behavior | bridge null → original runs | `StartGateAlertPatchTests.Prefix_...` (reflection invokes the prefix with `PatchBridge.Impl == null`) |
| Session-end residue | `_gate.ResetSessionState()` + `_hostStartGateAlertPending=false` | `GameAdapter.SessionEnded.cs`, `RunCoordinator.OnSessionEnded` |

## Verification design

L0 (development-period rule — no manual acceptance):

- `dotnet test`: 903 green, including the four new reflection tests and the
  existing patch-contract suite (which resolves `PlayerCamera.DoAlert` against
  the real game assembly).
- `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`: all pass.
- Static evidence: decompiled `WorldGeneration.cs` / `PlayerCamera.cs`
  line references above plus the `level1` scene serialized RectTransform
  anchor values.

Runtime trace design (for the next real dual-side pass):

- During the wait: `[Gate] deferring alert '<text>' until the gate release.`
- After release: `[Gate] replaying deferred alert '<text>' (important True).`
- A missing defer line on the host's layer-title popup, or a replay after
  `WorldReady`, is a regression.

Explicitly unverified until the next real pass: the visual look of the new
bottom-right panel (position/size are static-evidenced, not screenshot-
verified).
