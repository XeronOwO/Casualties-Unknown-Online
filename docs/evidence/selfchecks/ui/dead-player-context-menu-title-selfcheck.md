# Dead-player right-click context menu title — self-check

Owner cycle: `docs/backlog/todo/dead-player-right-click-name-suffix.md`
(now `review/`). The in-world right-click context menu showed only the
raw display name for a dead remote player; the dead state was already
available on `OnlineUiMemberRow.IsDead` but was not projected into the
context-menu title or the duplicate-name target selector buttons.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Shared member projection | `OnlineUiMemberProjection.Build` computes `IsDead = vitals is not null && !vitals.Alive` from the cached character snapshot. |
| 2 | Row data | `OnlineUiMemberRow` already exposes `Name` and `IsDead`; no new wire/protocol fact is needed. |
| 3 | Context menu rendering | `OnlineUiPlayerContextMenu.Draw` uses the same `OnlineUiMemberListDrawer.BuildRows` projection as the Players page and quick panel. |
| 4 | Localization | `LocalizationCatalog` provides en/zh key tables; the new `member.context_dead` key is context-menu-specific so the Players-list status label remains unchanged. |

## 2. Changes

- `OnlineUiMemberLabel.FormatContextTitle(displayName, isDead, deadSuffix)`
  is a pure label formatter: it appends the caller-provided localized suffix
  only when the projected row is dead.
- `OnlineUiPlayerContextMenu` now builds the menu title through that helper and
  also uses the same helper for the duplicate-name target selector buttons.
- `LocalizationCatalog` adds `member.context_dead`:
  - English: `(dead)`
  - Chinese: `(死掉了)`
- No wire message, protocol field, authority rule, or session state changed.

## 3. Whole-family audit

The same dead-state data is already rendered on the Players page status line
through `member.status_dead`; this change intentionally adds a separate
context-menu title suffix without altering that Players-list rendering. The
pure label formatter is the only new composition path, and both the context-menu
title and its target selector use it, so those two in-menu labels cannot drift.

## 4. L0 proof

- `OnlineUiMemberLabelTests.DeadMember_AppendsDeadSuffixToContextTitle`
  locks the dead path.
- `OnlineUiMemberLabelTests.LivingMember_DoesNotAppendDeadSuffixToContextTitle`
  locks the living no-suffix path.
- `OnlineUiMemberLabelTests.UnconsciousMember_DoesNotAppendDeadSuffixToContextTitle`
  locks the unconscious no-suffix path.
- The existing `OnlineUiMemberProjectionTests.DeadMemberExposesDeadStatusFlag`
  already covers `IsDead` from the shared projection.

Red step: before implementation, `DeadMember_AppendsDeadSuffixToContextTitle`
failed with `Expected: Alice(dead)` / `Actual: Alice`; the living/unconscious
tests passed. After implementation, all three passed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` | 2340 passed + 16 normative gates passed / 0 failed |
| Focused `OnlineUiMemberLabelTests` | 3 passed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| Deploy to game directory | completed |
| Deployed hash vs build output | `CasualtiesUnknownOnline.dll`, `Runtime.dll`, `Abstractions.dll`, `GameAdapter.dll`, `GameState.dll`, `Protocol.dll` all SHA-256 match and timestamps match |

## 6. Structure review

- No new service, mutable state, or UI framework change; the formatter is a
  pure static helper beside the existing Online UI projection types.
- Existing dead-detection remains only in `OnlineUiMemberProjection`; the new
  code consumes `row.IsDead`, so there is no duplicated/divergent rule.
- No class approached the architecture gate.

## 7. Remaining manual acceptance

The change is UI-only and the deployed plugin hash matches the build. The user
can verify in a real dual-client session: right-click a dead remote player from
either host or guest view (and a third-party view), and the context-menu title
should read the display name plus the localized dead suffix; living and
unconscious players must not get the suffix.
