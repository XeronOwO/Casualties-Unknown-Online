# Online UI window — full tabbed multiplayer UI (no protocol bump)

Date: 2026-08-23
Scope: replace the original top-left IMGUI status/lobby/member dump with a
full tabbed CUO Online window, add a top-right launcher, keep the world
nameplates/arrows and chat panel, and move the interaction-button eligibility
into a testable Runtime projection.

## What landed

- **OnlineUiWindow** (Plugin): a centered, draggable IMGUI window with tabs:
  Home, Lobby, Players, Network, Admin.
- **OnlineUiLauncher** (in `OnlineUiWindow`): a single top-right `CUO ONLINE`
  button; it replaces the old top-left scattered status panel as the entry
  point.
- **Home** — Steam status, create-lobby and join-by-ID, error text, hotkey
  hint.
- **Lobby** — lobby ID (with copy), role/owner/member count, member roster,
  host Kick/Ban actions.
- **Players** — in-world member roster with vitals, inventory expansion,
  Carry/Drop/Heal (+ explicit heal item), Take, Recruit actions.
- **Network** — Steam/lobby/session/entity-sync diagnostics and per-member RTT.
- **Admin** — host rules read-only summary and the persisted ban list with
  Unban.
- **OnlineUiMemberProjection** (Runtime): pure projection from session,
  vitals, inventory, player-interaction and host-ban surfaces into immutable
  member rows; the IMGUI drawers are dumb renderers.
- The old `DrawStatusPanel`/`DrawLobbyControls`/`DrawMemberStatus` top-left
  dump is deleted; nameplates/arrows and the bottom-right chat panel keep
  their presentation but now use the shared theme.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Lobby create/join | Same guarded `Plugin` entry points; UI only | `OnlineUiHomeDrawer` |
| Member interaction buttons | Eligibility moved into `OnlineUiMemberProjection` and still routes through existing `IPlayerInteractionControl`/`IGameAdapter` | `OnlineUiMemberRow` + 8 new projection tests |
| Chat | Unchanged behavior; same chat panel, styled | `OnlineUiOverlay.DrawChatPanel` |
| Nameplates/arrows | Unchanged behavior | `OnlineUiOverlay.DrawNameplatesAndArrows` |
| Admin kick/ban/unban | Same `SessionService.KickMember` / `IHostBanService` paths | `OnlineUiAdminDrawer` / `Plugin` delegates |
| Protocol | **No protocol/wire change** | no NetMsg touched |

## Verification design

- **L0 tests**: 8 new `OnlineUiMemberProjectionTests` cover lobby ordering,
  host admin eligibility, non-host exclusion, dead/unconscious carry+take,
  alive heal, dead recruit, carry→drop and ban marking.
- **Build + gates**: `dotnet build` 0 warnings/errors; `dotnet format`;
  `check-architecture.ps1` pass; full suite 1286 tests green.
- **Runtime smoke**: launched the real game once after deploy — plugin loaded,
  Game Adapter installed and verified (132 targets), no CUO-originated
  exception in `latest.log` (the only error is the pre-existing HotRepl
  `System.ComponentModel.DataAnnotations` TypeLoadException, unrelated to CUO).
- Static evidence: all new UI code reads existing public Runtime surfaces; no
  sync/protocol path touched; no new game-assembly code added outside the
  Plugin's existing IMGUI boundary.
- No manual dual-side acceptance: per the development-period rule this is
  verified with L0 tests + static evidence + launch smoke.

## Accepted limitations

- Still IMGUI, not a UGUI canvas — this is the deliberate dedicated UI pass
  that the earlier online-ui self-check reserved; a runtime UGUI redesign can
  follow later.
- The main-menu entry is a top-right IMGUI launcher, not yet a cloned native
  `AdaptiveButton` in the game's own main-menu list.
- UI labels are English; no localization integration yet.
- Host rules remain read-only in the UI (BepInEx config is still the edit
  path).
