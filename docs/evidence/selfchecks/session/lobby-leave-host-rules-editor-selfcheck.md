# Lobby leave / close + host rules in-game editor (no protocol bump)

Date: 2026-08-23
Scope: add explicit disconnect/close-room controls to the Online UI and let a
host edit host/respawn rules directly from the Admin page instead of only
reading them.

## What landed

- **Leave Lobby / Close Room**
  - `SteamService.LeaveLobby()` exposes the existing internal
    leave-current-lobby path as a public UI entry point. It fires `LobbyLeft`
    so the session layer tears down normally.
  - `OnlineUiLobbyDrawer` adds a button on the Lobby page: guests see
    **Leave Lobby**, host sees **Close Room**.
  - Button text is localized (`lobby.leave_lobby` / `lobby.close_room`).
- **Host rules editor**
  - `HostRulesConfigEditor` (Plugin) holds the actual BepInEx
    `ConfigEntry<bool>` references for the host-rule/respawn flags and writes
    them through the same entries the runtime `IOptionsMonitor` reads.
  - `OnlineUiAdminDrawer` shows a toggle per rule when the local player is a
    host; guests keep the read-only summary.
  - Editable flags: PvP, auto-continue, allow late join, keep inventory,
    revive from trader, revive on next level, permadeath.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Lobby lifecycle | `SteamService.LeaveLobby()` → existing `LeaveCurrentLobbyFor` → `LobbyLeft` → session teardown | `SteamService`, `SessionService` |
| UI | Lobby page Leave/Close button | `OnlineUiLobbyDrawer` |
| Config write | `HostRulesConfigEditor.Set*` sets `ConfigEntry.Value` + `ConfigFile.Save()` | `HostRulesConfigEditor` |
| Config read | Toggle state comes from runtime `IHostRules` / `IOptionsMonitor` | `OnlineUiAdminDrawer`, `HostRulesService` |

## Verification design

- **Build + gates**: `dotnet build` 0 warnings/errors; `dotnet format`;
  `check-architecture.ps1` pass; full suite green.
- **Runtime smoke**: real-game launch after deploy, plugin loads with no CUO
  exception.
- No protocol/wire change; the leave path and config writes exercise existing
  session/config machinery.

## Accepted limitations

- Host rules are local host config only; they are not sent to guests.
- `KeepSkills` is not exposed in the Admin page yet (it still has an existing
  config entry; only the currently displayed rule set is editable).
