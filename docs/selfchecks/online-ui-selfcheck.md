# Online UI self-check

Date: 2026-08-18
Scope: the remaining `Online UI` backlog item — lobby create/join controls,
member status, and world nameplates + off-screen arrows.

## What landed

- **OnlineUiOverlay** (Plugin, IMGUI): replaces the temporary Phase-1 test HUD
  with the Online UI panel. It owns:
  - a lobby ID text field + **Join** / **Create Lobby** buttons (same guarded
    paths as the F8/F9 hotkeys, `Plugin.TryJoinLobbyFromUi` /
    `Plugin.TryCreateLobbyFromUi`);
  - a member status list — persona / SteamID / host-or-guest / handshake /
    in-world-or-menu for every lobby member;
  - world nameplates for in-world remote players, plus screen-edge arrows for
    off-screen players.
- **SteamService.GetPersonaName** (Runtime): Steam persona lookup for the local
  user and lobby members; added to `ISteamService` and `FakeSteamService` so
  the abstraction boundary stays Steamworks-free.
- **OffScreenArrowGeometry** (Runtime): pure GUI-coordinate edge math for the
  on-screen/off-screen decision and arrow pinning — no UnityEngine dependency,
  covered by L0 tests.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Lobby create/join | UI buttons call the existing guarded entry points (`EnsureSteamReady` + `CanSwitchLobbyForCreate/Join`); no protocol change | `Plugin.cs` `TryCreateLobbyFromUi` / `TryJoinLobbyFromUi`, `OnlineUiOverlay.DrawLobbyControls` |
| Lobby lifecycle / roles | Unchanged; the overlay reads `SteamService` + `SessionService` state | `SteamService.GetLobbyOwner/GetLobbyMembers`, `SessionService.Role/SessionActive/Members` |
| Member status | New display surface; persona names added to `ISteamService` | `SteamService.GetPersonaName` (`SteamFriends.GetPersonaName` / `GetFriendPersonaName`) |
| Nameplates / off-screen arrows | New rendering surface; pure geometry extracted | `OffScreenArrowGeometry` + 8 L0 tests |
| Waiting overlay (start gate, #87) | Unchanged; still drawn before the Online UI panel | `Plugin.OnGUI` |

## Verification design

- **L0 simulation/unit tests**: 8 new `OffScreenArrowGeometryTests` cover
  on-screen, four edge directions, corner pinning and invalid bounds. Full
  suite: **973 tests green** (was 965).
- **Build + gates**: `dotnet build` 0 warnings/errors; `dotnet format`;
  `check-architecture.ps1` pass (one top-level type per file);
  `check-event-replay.ps1` pass; `check-entity-event-dispatch.ps1` pass.
- **Static evidence**: `OnlineUiOverlay` only reads existing public state and
  calls `Camera.main.WorldToScreenPoint` in the IMGUI pass; no sync/protocol
  surface changed (no protocol bump).
- No manual dual-side acceptance: per the development-period rule this is
  verified with L0 tests + static evidence.

## Accepted limitations

- IMGUI rather than a custom Unity UI canvas — matches the existing on-screen
  overlay pattern and keeps the feature self-contained; a canvas redesign can
  follow as a dedicated UI pass.
- Persona names come from Steam's persona API; when Steam returns an empty
  name the overlay falls back to `player-<SteamID in hex>`.
- Nameplates render only for remote players whose current `PlayerEntity`
  position is known and whose session presence is `InWorld`; host-only/local
  players are intentionally skipped.

## Structure review

- `OnlineUiOverlay.cs` (~200 lines, one top-level type, no state bools above
  threshold).
- `OffScreenArrowGeometry.cs`, `OffScreenArrowDirection.cs`,
  `OffScreenArrowPlacement.cs` — one top-level type each.
- `Plugin.cs` remains under 600 lines and stays a thin lifecycle driver.