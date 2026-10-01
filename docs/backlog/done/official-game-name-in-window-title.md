# The window title must use the game's official Chinese name

- Status: Done
- Acceptance (20261001-j): both rows pass (the Chinese title reads `未知伤亡：联机`; the English one is unchanged) — record `docs/evidence/acceptance/official-game-name-in-window-title-20261001.md`
- Priority: Medium
- Category: Localization / Online UI
- Source: User correction (2026-09-27): the game's official Chinese name is 未知伤亡, not the reversed
  伤亡未知 the Online UI's window title read.
- Related: `todo/online-ui-layout-and-input-detail-pass.md` (the window the string is read on),
  `docs/standard/terminology.txt`

## Problem

The Online UI's Chinese window title read `伤亡未知：联机`. The game's official Chinese name is `未知伤亡`,
so a Chinese client showed the name reversed on the window every player opens.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | A Chinese client opens the Online UI window | The title reads `未知伤亡：联机` |
| 2 | An English client opens the Online UI window | The title is unchanged (`CASUALTIES UNKNOWN: ONLINE`) |

## What landed

- `LocalizationCatalog.Chinese["window.title"]` carries `未知伤亡：联机`.
- `LocalizationServiceTests.TheWindowTitleUsesTheGamesOfficialChineseName` pins the official name with the
  correction and its date, so the reversed form cannot come back.

## Limits

- The mod's own window title is the string in scope; the game's own menus are the game's text, not CUO's.
- The frame was captured from the deployed build of the 2026-09-27 run (`ui2-fixed-*` in the directory
  `AGENTS.local.md` names); the next acceptance batch re-deploys the committed artifact and judges the rows.
