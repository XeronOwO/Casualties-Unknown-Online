# Remove the duplicated command console page from the Online UI window

- Status: Todo
- Priority: Low-Medium
- Category: Online UI / command console
- Source: User finding and ruling (2026-09-21): asked whether the window's console page is worth keeping next to the in-game command line, the user ruled: delete the tab.
- Related: `review/in-game-command-console-interactive.md`, `done/in-game-command-console.md`, `review/command-tree-resource-location-selector.md`

## Evidence

- `OnlineUiConsoleDrawer` renders `ctx.Commands.Lines` and one text field, and calls
  `ctx.Commands.TryExecute(input)` — the same `CommandConsoleService` the in-game overlay uses.
- The overlay (`CommandConsoleOverlay` plus `ConsoleInputSession` and `CommandConsoleInputRenderer`)
  is opened with `/` (`OnlineUiHost`) and owns completion, history, suggestions and the
  notification lines; chat send goes through the same service on both surfaces.
- The window page therefore has no capability the in-game console lacks; the second surface is a
  copy whose only cost is a second place to keep in sync.

## Requirement

Delete the page: the console tab, the page enum member, the window-state fields it used, the drawer
and the localisation keys that become unused. The in-game `/` console stays the only command and
chat surface; the user explicitly declined a replacement button in the window.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Open the Online UI window | No Console tab; the other tabs unchanged |
| 2 | Press `/` | The in-game console opens with completion, history and suggestions |
| 3 | Type a command or a chat line in it | Works exactly as before |
| 4 | Search the tree | No unused console page, drawer, enum member, window state or localisation key remains |
| 5 | Build, tests, gates, format | Green |

## Non-goals

- Not changing the command registry, the chat domain or the console input session.
- Not adding a different in-window console.
