# Member status icons + session hotkey config self-check

Owner cycle: backlog lower-priority KrokMP candidates "status icons" and
"co-op keybinds". Decision: implement both as small, pure-projection/UI/config
slices — no wire message, no protocol change.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Online UI member rows | `OnlineUiMemberProjection` / `OnlineUiMemberListDrawer` — existing row cards |
| 2 | Vitals status | `RemoteVitalsSnapshot.Alive/Conscious` from `CharacterHealthMsg` |
| 3 | Carry relations | `IPlayerInteractionControl.TryGetCarrier/TryGetCarried` — already used for button eligibility |
| 4 | Session hotkeys | `Plugin.Update` previously hardcoded `KeyCode.F8/F9/F7`; BepInEx config entries now control them |
| 5 | Localization | `ILocalizationService` key-based tables (en/zh) |

## 2. Design

- `OnlineUiMemberRow` gains four read-only status booleans: `IsDead`,
  `IsUnconscious`, `IsCarryingSomeone`, `IsCarried`. The projection fills them
  from the same cached vitals and carry relations already used for action
  eligibility; no new service or protocol surface.
- `OnlineUiMemberListDrawer.BuildStatus` appends localized status tags to the
  existing member status line, so the Players page and Lobby page both show the
  current body/carry state at a glance.
- `Plugin` binds three `[Session]` string config entries
  (`CreateLobbyKey` / `JoinLobbyKey` / `PingPeerKey`). Values use
  `UnityEngine.KeyCode` names; a failed/unknown value simply disables that
  hotkey instead of throwing. Defaults keep the historical F8/F9/F7 behavior.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Dead status | projection sets `IsDead` when vitals show `Alive=false` | `DeadMemberExposesDeadStatusFlag` |
| Unconscious status | projection sets `IsUnconscious` when alive but `Conscious=false` | `UnconsciousMemberExposesUnconsciousStatusFlag` |
| Carry/carried status | projection mirrors carry relations | `CarryRelationExposesCarryingAndCarriedFlags` |
| Hotkey config | hardcoded keys replaced by parse-on-press config values | static source + existing hotkey path unchanged otherwise |
| Localization | new `member.status_*` keys in en/zh | `LocalizationServiceTests` existing table round-trip |

## 4. Verification

- **L0 unit**: `OnlineUiMemberProjectionTests` +3 status-flag cases.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
