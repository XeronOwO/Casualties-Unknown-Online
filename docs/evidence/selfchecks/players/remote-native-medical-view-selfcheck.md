# Remote native medical (WoundView) view — self-check

Closes the backlog item `remote-player-medical-panel.md`.
Previous cycle built a CUO-side IMGUI medical panel and was rejected because it
did not reuse the game's existing medical UI. This cycle reuses the game's own
`WoundView` health panel and feeds it a display-only body copy built from the
already-synced 1 Hz character snapshot.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Game's native medical UI | `WoundView.UpdateView` reads a `Body` and renders full health/limb text, bars, icons and body diagram (`reversing/.../WoundView.cs`) |
| 2 | Full remote health/limb data on the wire | `CharacterDataMsg.Health` / `CharacterDataMsg.Limbs` already travel on the 1 Hz character snapshot; `CloneData` holds the latest per-SteamID copy |
| 3 | Online UI entry point | `IGameAdapter.OpenRemoteMedical` / `OnlineUiOverlay.OpenRemoteMedical` replace the rejected CUO IMGUI panel with the native view |
| 4 | Display-only body copy | `RemoteMedicalCoordinator.TryCreateDisplayBody` clones the "Experiment" template, deactivates it, freezes physics, and maps the remote snapshot onto it |
| 5 | Read-only interaction guard | `RemoteMedicalPatches` blocks `TakeANap`, radial use/wear, and special limb actions while the remote medical view is open |
| 6 | Cleanup | `RemoteMedicalView.Close` restores the native panel to the local body, clears the selected limb, and destroys the display copy; `RemoteMedicalCoordinator.Update` also tears down on session/world/panel close |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| Custom CUO IMGUI panel | `OnlineUiMedicalPanel.cs` removed; no parallel presentation path remains |
| Online UI member list/context menu | both still expose the same "Medical" action, now routed to the native WoundView |
| Remote vitals cache | unchanged; still feeds the member status line and remains the data source behind the native view |
| Remote render clone | not mutated by this path — the native view uses a separate inactive display body |
| Wire/protocol | unchanged — no new messages or fields |
| Authority | unchanged — display-only, no host-authoritative operation introduced |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Runtime boundary exposes native medical surface | `IGameAdapter.OpenRemoteMedical` / `CloseRemoteMedical` exist and return the expected signatures | `RemoteMedicalContractTests` |
| GameAdapter implements the surface | `GameAdapter` implements `IGameAdapter` | `RemoteMedicalContractTests` |
| Adapter-side remote focus remains a bounded static surface | `RemoteMedicalView` exposes Open/Close/IsOpen with a display body | `RemoteMedicalContractTests` |
| No dead custom panel remains | file deleted; no source references to `OnlineUiMedicalPanel` | grep/source audit |
| Native interaction actions are blocked in remote mode | new Harmony prefixes on `WoundView.TakeANap`, `PlayerCamera.TryPerformRadialAction`, `TryPerformSpecialUIAction` | `RemoteMedicalPatches.cs` |
| Cleanup happens on user close | `PlayerCamera.ToggleWoundView` postfix calls `RemoteMedicalView.Close` when the panel deactivates | `RemoteMedicalPatches.cs` |
| Session/world exit cleans up | `RemoteMedicalCoordinator.Update` closes when session/world/remote leaves | `RemoteMedicalCoordinator.cs` |

## 4. Verification

- **Build**: `dotnet build CasualtiesUnknownOnline.slnx --no-restore` — 0 warnings / 0 errors.
- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` — 2278 passed / 0 failed.
- **Format**: `dotnet format CasualtiesUnknownOnline.slnx --no-restore` run.
- **Gates**: `check-architecture.ps1`, `check-event-replay.ps1`, `check-entity-event-dispatch.ps1`, `check-delivery.ps1` all pass.
- **Protocol**: unchanged (no protocol bump).

## 5. Structure review

- New top-level types are single-purpose: `RemoteMedicalView` (static focus state), `RemoteMedicalCoordinator` (bridge + display copy), `RemoteMedicalPatches` (Harmony seams).
- No touched file approaches the line/state gates; no new mutable domain state beyond the session-scoped static remote focus.
- The custom panel deletion is a real removal, not an unused-file hiding.
