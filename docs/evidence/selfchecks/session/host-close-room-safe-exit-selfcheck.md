# Host close-room safe exit — delivery fact sheet

Status: delivered. The host's explicit Close Room / session teardown no longer
loads the main menu synchronously from a Steam/UI callback; the menu return is
deferred to the next Update pump and the host's native run save is persisted
first. Build + format + architecture/event-replay/entity-dispatch/delivery
gates pass, 1838 tests green (L0), runtime verification = L0 + static evidence,
no manual acceptance. Final dual-side acceptance remains a user release action.

Cycle: backlog open bug "Host closing the lobby exits the game and can destroy
the run save" (2026-08-27).

## What landed

- `RunCoordinator.OnSessionEnded` / `OnRemoteSceneChanged` no longer call
  `PlayerCamera.main.ToMainMenu()` directly from a session event. They record a
  one-shot `RunMenuReturnRequest`; `RunCoordinator.Update` consumes it on the
  normal Unity Update pump and performs the scene load there.
- The host path is the only save-authority path: `RunMenuReturnPolicy` returns
  `SaveAndMenu` for a host leaving a live world and `MenuOnly` for a guest.
  `FlushMenuReturn` calls `SaveSystem.SaveGame()` before `ToMainMenu()` for the
  host, so closing the room preserves the native run save instead of dropping
  the player into the menu without a save.
- A stale request is discarded if a new session activated before the pump (the
  request belongs to the torn-down session, not a new one).

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Session teardown → menu return | Deferred via `RunMenuReturnRequest`, consumed by `RunMenuReturnCoordinator.Flush` on the Update pump | `RunCoordinator.cs:137,471,490`, `RunMenuReturnRequest.cs` |
| Save authority | `RunMenuReturnPolicy.Decide(role, inWorld)`; host = `SaveAndMenu`, guest = `MenuOnly` | `RunMenuReturnPolicy.cs`, `RunMenuReturnPolicyTests` |
| Native run save | `SaveSystem.SaveGame()` called only on the host before `ToMainMenu()` | `RunMenuReturnCoordinator.cs:58,68` |
| Stale-request guard | Flush skips when a new session is already active | `RunMenuReturnCoordinator.cs:49-52` |
| Guest host-loss pull | Guest still returns to menu, but deferred to the Update pump | `RunCoordinator.cs:468-471` |

## Root cause addressed

The previous code called `PlayerCamera.main.ToMainMenu()` synchronously from
`SessionEnded`. That event is fired inside `SteamService.LeaveCurrentLobbyFor`,
which is reached from the Online UI's IMGUI button handler (OnGUI). Loading a
scene inside that callback path was the host-close exit/freezing hazard, and the
path also returned to the menu without persisting the native run save. The fix
separates "record the teardown" from "perform the scene transition" and makes
the save explicit.

## Verification design (development period — no manual acceptance)

- `RunMenuReturnPolicyTests`: host in world → `SaveAndMenu`; guest in world →
  `MenuOnly`; not in world → `None`.
- `RunMenuReturnRequestTests`: one-shot consume, mode retained, `None` never
  arms.
- `dotnet build` 0 warnings/errors; `dotnet format`; `check-architecture`,
  `check-event-replay`, `check-entity-event-dispatch`, `check-delivery` all
  pass.
- Full suite: 1838/1838 green.
- Static evidence: no new wire/protocol change, no Harmony/game assembly
  reference added to Runtime, no direct scene-load call remains in session
  event handlers.
- Structure review: `RunCoordinator` stays under the 600-line gate; new Runtime
  types are one top-level type per file; no new expression-state bools beyond
  gate.

## Explicitly not touched

- Protocol/wire: no message changes, `ProtocolVersion.Current` unchanged.
- IP-direct leave and Steam lobby lifecycle semantics unchanged.
- Guest save authority unchanged; guests never write the native run save during
  teardown.
- Final dual-side acceptance remains a user release action.
