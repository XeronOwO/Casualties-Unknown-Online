# Remove the duplicated command console page from the Online UI window

- Status: Review
- Priority: Low-Medium
- Category: Online UI / command console
- Source: User finding and ruling (2026-09-21): asked whether the window's console page is worth keeping next to the in-game command line, the user ruled: delete the tab.
- Related: `review/in-game-command-console-interactive.md`, `done/in-game-command-console.md`, `review/command-tree-resource-location-selector.md`, `docs/evidence/selfchecks/ui/online-ui-console-page-removal-selfcheck.md`

## Evidence

- `OnlineUiConsoleDrawer` renders `ctx.Commands.Lines` and one text field, and calls
  `ctx.Commands.TryExecute(input)` — the same `CommandConsoleService` the in-game overlay uses.
- The overlay (`CommandConsoleOverlay` plus `ConsoleInputSession` and `CommandConsoleInputRenderer`)
  is opened with `/` (`OnlineUiHost`) and owns completion, history, suggestions and the
  notification lines; at the time of the ruling chat send went through the same service on both
  surfaces (today the overlay is the only surface).
- The window page therefore had no capability the in-game console lacks; the second surface was a
  copy whose only cost was a second place to keep in sync.

## Requirement

Delete the page: the console tab, the page enum member, the window-state fields it used, the drawer
and the localisation keys that become unused. The in-game `/` console stays the only command and
chat surface; the user explicitly declined a replacement button in the window.

## What landed (2026-09-26 cycle)

The page is gone end to end and the in-game `/` overlay is the only command and chat surface.

- **The page shell is deleted** — `OnlineUiConsoleDrawer.cs` is gone, `OnlineUiPage` declares the six
  surviving pages (`Home`, `Players`, `Network`, `Admin`, `Worlds`, `Preferences`), and
  `OnlineUiWindow` draws exactly one tab and one switch case per page, in enum order.
- **The page's local state went with it** — `OnlineUiWindowState.ConsoleInput` is deleted; the drawer
  held its only reader and writer.
- **The catalogue is back to the keys the tree uses** — `tab.console`, `console.title` and
  `console.send` are deleted from both the English and the Chinese dictionary with the page, and the
  same census found two keys nothing had referenced any more (`console.hint`,
  `console.overlay.controls`), which are deleted rather than left as orphans. The in-game overlay
  keeps its own `console.overlay.empty`.
- **The contract is pinned** — `OnlineUiConsolePageRemovalPinTests` reads the page shell and the
  catalogue as text and asserts: the enum body, the tab row and the switch are exactly the six
  surviving pages; the drawer type and the state field are gone from the plugin source; and the
  catalogue's `tab.`/`console.` key set equals the keys some source file references, in both
  languages, so an orphan key and a dangling reference both fail. Eight negative samples re-add one
  half of the page each (enum member, tab call, a dropped tab, switch case, drawer reference, state
  field, orphan key, dangling reference) and must each be rejected.

No wire protocol, save shape, host rule or command/chat behaviour changed.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | Open the Online UI window | No Console tab; the other six tabs unchanged | the pin (enum census, tab row, switch); the rendered frame is the user's run |
| 2 | Press `/` | The in-game console opens with completion, history and suggestions | the overlay path is untouched; `ConsoleInputSessionTests`; the user's run |
| 3 | Type a command or a chat line in it | Works exactly as before | `CommandConsoleService` untouched; its existing tests; the user's run |
| 4 | Search the tree | No unused console page, drawer, enum member, window state or localisation key remains | the pin's two-direction censuses; selfcheck §1 |
| 5 | Build, tests, gates, format | Green | selfcheck §6 |

## Evidence

- Selfcheck: `docs/evidence/selfchecks/ui/online-ui-console-page-removal-selfcheck.md`
- Deleted: `src/CasualtiesUnknownOnline.Plugin/OnlineUiConsoleDrawer.cs`
- Page shell: `src/CasualtiesUnknownOnline.Plugin/OnlineUiPage.cs`,
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiWindow.cs`,
  `src/CasualtiesUnknownOnline.Plugin/OnlineUiWindowState.cs`
- Catalogue: `src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs`
- Pin: `tests/CasualtiesUnknownOnline.Tests/OnlineUi/OnlineUiConsolePageRemovalPinTests.cs`

## Limits

- The suite proves the source shape and the key census; it cannot draw a frame. Rows 1-3 — the tab
  row as rendered, the `/` overlay still opening with completion, history and suggestions, and a
  command or chat line still working — are the user's real-session rows.
- The key census covers the `tab.` and `console.` prefixes, which is where the page's keys live; an
  orphan key in another family is outside its reach and would need its own census.
- `console.hint` and `console.overlay.controls` had no reader anywhere in the tree at HEAD, before
  this cycle's deletion, so the two `console.*` keys the page family left behind are removed with it.
  Other unreferenced families are untouched: the same census finds 81 declared-and-unreferenced keys
  elsewhere in the catalogue (59 `medical.*`, 8 `prefs.*`, 3 `member.*`, 3 `hud.*`, 3 `common.*`, and
  one each of `chat.`, `home.`, `ip.`, `lobby.`, `players.`), recorded as
  `todo/catalogue-and-manifest-census-drift.md` so that policy is decided once instead of per cycle.
  Nothing renders the overlay's controls line from the catalogue today, which this cycle records
  instead of changing the overlay.

## Non-goals

- Not changing the command registry, the chat domain or the console input session.
- Not adding a different in-window console.
