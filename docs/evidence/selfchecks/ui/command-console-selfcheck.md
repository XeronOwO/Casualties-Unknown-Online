# In-game command console — self-check

Owner cycle: interactive in-game command console. This selfcheck covers the
final behavior: a slash-opened standalone console in addition to the modal
Online UI console page. The command execution chain remains local and uses the
existing text-chat send path; no wire message or protocol version was added.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Command chain | `CommandConsoleService` owns registration, slash parsing, role permission, execution and output buffering. |
| 2 | Command metadata | `CommandSpec` + `ICommandCompletionSource` expose name/description/usage/permission/argument kinds to the UI without exposing handlers. |
| 3 | Completion suggestions | `CommandConsoleService.Suggest` returns `CommandSuggestion` items (text + description) for command names, member names/SteamIds, and banned SteamIds based on argument kind. |
| 4 | Rich hint UI | The standalone overlay renders a scrollable, clickable suggestion list; `GUIContent` tooltips show the candidate description on hover. |
| 5 | Tokenizer | `CommandLineTokenizer` splits commands while preserving double/single quotes, escapes, `[]`/`{}`/`()` groups, so spaced values, selectors and JSON-like literals survive as one token. |
| 5 | Input state machine | `ConsoleInputSession` owns open/close, current line, history navigation, completion cycling and submission, all Unity-free. |
| 6 | Fade policy | `ConsoleFadePolicy.ComputeAlpha` provides the hold/fade curve used by both console surfaces. |
| 7 | Standalone overlay | `CommandConsoleOverlay` draws a text area + focused input field only when the input session is open, independent of the Online UI window. |
| 8 | Slash hotkey / modal routing | `Plugin.Update` opens the console on `KeyCode.Slash` and calls `IGameAdapter.SetOnlineUiModal` for the console as well as the Online UI window. |
| 9 | Input blocking / ESC | The existing `OnlineMenuInputGuard`, `PlayerCameraHandleInputPatch` and `PauseHandlerTogglePausePatch` suppress background UI/game input while modal; the overlay consumes Escape before the game sees it. |
| 10 | Console page polish | `OnlineUiConsoleDrawer` now applies the same fade to its lines; the standalone overlay shows hints and completion candidates. |
| 11 | No wire change | No `NetMsg`, no packet handler, no `ProtocolVersion` change. |

## 2. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Help lists commands | `/help` returns command names/descriptions | `CommandConsoleServiceTests.Help_ReturnsAvailableCommands` |
| Unknown command handled | Unknown slash command adds an error line | `CommandConsoleServiceTests.UnknownCommand_AddsErrorLine` |
| Chat send through console | Plain text goes through `IChatControl` and echoes in console output | `CommandConsoleServiceTests.PlainText_SendsChatAndEchoesInConsole` |
| Host-only permission | Guest `/kick` gets a host-only refusal | `CommandConsoleServiceTests.HostOnlyCommand_IsRefusedForGuest` |
| Kick/ban/unban commands | Host admin commands round-trip through real session/ban paths | `CommandConsoleServiceTests.HostKick_RemovesMember`, `HostBan_And_Unban_RoundTrip` |
| Output bounded | Buffer caps at `MaxLines` | code review: `AddLine` removes oldest |
| Command-name completion | `/k` suggests `kick` | `CommandConsoleServiceTests.Suggest_ReturnsCommandNamesForPrefix` |
| Rich suggestion descriptions | Command suggestions carry a non-empty description | `CommandConsoleServiceTests.Suggest_IncludesDescriptionForCommand` |
| Specific help command | `/help kick` shows the command usage | `CommandConsoleServiceTests.Help_WithCommandName_ShowsUsage` |
| Clickable suggestion acceptance | Clicking a suggestion applies it and clears the list | `ConsoleInputSessionTests.AcceptSuggestion_AppliesSpecificCandidateAndClearsList` |
| Member completion | `/kick <id>` suggests the member id | `CommandConsoleServiceTests.Suggest_ReturnsMemberIdForKickArgument` |
| Usage hint | `/kick` returns a usage hint | `CommandConsoleServiceTests.GetHint_ReturnsUsageForKnownCommand` |
| Tokenizer quoting | Quoted spaces remain one token and unquote cleanly | `CommandLineTokenizerTests.Tokenize_RespectsDoubleQuotedSpaces`, `Unquote_StripsMatchingQuotes` |
| Tokenizer JSON/selector group | Brace groups stay one token even with spaces | `CommandLineTokenizerTests.Tokenize_KeepsBraceGroupAsSingleToken` |
| Fade policy | Hold, mid-fade, and expired alphas are deterministic | `ConsoleFadePolicyTests` (4 cases) |
| Input session open/close | Open prefills `/`; Escape closes without executing | `ConsoleInputSessionTests.Open_PrefillsSlashAndSetsOpen`, `Escape_ClosesWithoutExecuting` |
| Input session submit/history | Submit keeps console open, records history, Up/Down restores draft | `ConsoleInputSessionTests.Submit_ExecutesClearsAndKeepsConsoleOpen`, `History_UpAndDown_RestoresDraft` |
| Input session completion | Command-name and spaced-argument completion quote correctly | `ConsoleInputSessionTests.CycleCompletion_CompletesCommandName`, `CycleCompletion_QuotesSpacedArgument` |
| Focus/mouse block/ESC in Unity | Focus enforcement, modal routing and ESC consumption are in the overlay/plugin path | static review: `CommandConsoleOverlay.EnsureFocus`, `Plugin.Update`, `OnlineUiOverlay`; Unity IMGUI behavior is user-acceptance territory |

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1888 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass (including GameState isolation, item authority, no-legacy, command authority, kernel shape) |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
| Protocol | unchanged |

## 4. Structure review

- `CommandConsoleService` remains under the line-count gate and now implements
  both `ICommandControl` and `ICommandCompletionSource`; the completion data is
  still owned by the command registry.
- `ConsoleInputSession` is one top-level type, Unity-free, and owns all
  interactive input state; the Unity overlay is a thin presenter.
- `CommandConsoleOverlay` is one top-level type and owns only IMGUI drawing/event
  translation; it does not own command/history policy.
- `CommandLineTokenizer` and `ConsoleFadePolicy` are pure static helpers with no
  mutable state.
- No new booleans exceeding the architecture gate; no dead mechanisms left behind.
