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
| 5 | Argument provider seam | `ICommandArgumentSuggestions` exposes per-kind providers for command names, players/SteamIds, banned SteamIds, selectors and JSON scaffolds. |
| 6 | Tokenizer | `CommandLineTokenizer` splits commands while preserving double/single quotes, escapes, `[]`/`{}`/`()` groups, so spaced values, selectors and JSON-like literals survive as one token. |
| 7 | Input state machine | `ConsoleInputSession` owns open/close, current line, history navigation, completion cycling and submission, all Unity-free. |
| 8 | Cursor-aware input | `ConsoleInputSession` owns the cursor and editing operations; the overlay renders a custom IMGUI field with caret, arrow/Home/End/Backspace/Delete, Ctrl+Left/Right, Ctrl+Backspace/Ctrl+Delete, and click-to-place cursor. |
| 9 | Basic highlighting | The custom input renders command tokens in accent, quoted/selector/JSON-like literals in muted, and plain text in default color. |
| 10 | Fade policy | `ConsoleFadePolicy.ComputeAlpha` provides the hold/fade curve used by both console surfaces. |
| 11 | Standalone overlay | `CommandConsoleOverlay` draws a text area + focused input field only when the input session is open, independent of the Online UI window. |
| 12 | Slash hotkey / modal routing | `Plugin.Update` opens the console on `KeyCode.Slash` and calls `IGameAdapter.SetOnlineUiModal` for the console as well as the Online UI window. |
| 13 | Input blocking / ESC | The existing `OnlineMenuInputGuard`, `PlayerCameraHandleInputPatch` and `PauseHandlerTogglePausePatch` suppress background UI/game input while modal; the overlay consumes Escape before the game sees it. |
| 14 | Console page polish | `OnlineUiConsoleDrawer` now applies the same fade to its lines; the standalone overlay shows hints and completion candidates. |
| 15 | Selection/clipboard | `ConsoleInputSession` owns selection ranges; the overlay renders selection highlight and wires Ctrl+A/C/X/V to the Unity system clipboard. |
| 16 | No wire change | No `NetMsg`, no packet handler, no `ProtocolVersion` change. |

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
| Argument provider selectors | `Selector` argument kind returns `@a`/`@p`/... candidates | `CommandConsoleServiceTests.ArgumentSuggestions_SelectorKind_ReturnsSelectors` |
| Argument provider JSON | `Json` argument kind returns JSON scaffold candidates | `CommandConsoleServiceTests.ArgumentSuggestions_JsonKind_ReturnsTemplates` |
| Specific help command | `/help kick` shows the command usage | `CommandConsoleServiceTests.Help_WithCommandName_ShowsUsage` |
| Clickable suggestion acceptance | Clicking a suggestion applies it and clears the list | `ConsoleInputSessionTests.AcceptSuggestion_AppliesSpecificCandidateAndClearsList` |
| Member completion | `/kick <id>` suggests the member id | `CommandConsoleServiceTests.Suggest_ReturnsMemberIdForKickArgument` |
| Usage hint | `/kick` returns a usage hint | `CommandConsoleServiceTests.GetHint_ReturnsUsageForKnownCommand` |
| Tokenizer quoting | Quoted spaces remain one token and unquote cleanly | `CommandLineTokenizerTests.Tokenize_RespectsDoubleQuotedSpaces`, `Unquote_StripsMatchingQuotes` |
| Tokenizer JSON/selector group | Brace groups stay one token even with spaces | `CommandLineTokenizerTests.Tokenize_KeepsBraceGroupAsSingleToken` |
| Token at cursor | A token under an arbitrary cursor is found, whitespace returns empty | `CommandLineTokenizerTests.TokenAtCursor_ReturnsTokenUnderCursor`, `TokenAtCursor_ReturnsEmptyAtWhitespace` |
| Cursor editing | Insert/backspace/move cursor update input+cursor correctly | `ConsoleInputSessionTests.InsertChar_InsertsAtCursorAndMovesForward`, `Backspace_DeletesBeforeCursor`, `MoveCursorLeftAndRight_AdjustsPosition` |
| Cursor-aware completion | Tab replaces the token at the cursor and preserves suffix | `ConsoleInputSessionTests.CycleCompletion_ReplacesTokenAtCursorPreservingSuffix` |
| Word editing | Ctrl+Backspace/Ctrl+Delete and Ctrl+Left/Right work on word boundaries | `ConsoleInputSessionTests.BackspaceWord_DeletesWholeWordBeforeCursor`, `DeleteWord_DeletesNextWordAfterCursor`, `MoveWordLeftAndRight_JumpsBetweenWords` |
| Selection | Select-all, selected text, insert/delete replacing selection, Shift+arrow extension | `ConsoleInputSessionTests.SelectAll_HasSelectionAndSelectedText`, `InsertText_ReplacesSelection`, `DeleteSelection_RemovesRange`, `MoveCursorRight_WithShiftExtendsSelection` |
| Clipboard wiring | Ctrl+C/X/V use the Unity system clipboard in the overlay | static review: `CommandConsoleOverlay` Ctrl+C/X/V cases; clipboard behavior is user-acceptance territory |
| Fade policy | Hold, mid-fade, and expired alphas are deterministic | `ConsoleFadePolicyTests` (4 cases) |
| Input session open/close | Open prefills `/`; Escape closes without executing | `ConsoleInputSessionTests.Open_PrefillsSlashAndSetsOpen`, `Escape_ClosesWithoutExecuting` |
| Input session submit/history | Submit keeps console open, records history, Up/Down restores draft | `ConsoleInputSessionTests.Submit_ExecutesClearsAndKeepsConsoleOpen`, `History_UpAndDown_RestoresDraft` |
| Input session completion | Command-name and spaced-argument completion quote correctly | `ConsoleInputSessionTests.CycleCompletion_CompletesCommandName`, `CycleCompletion_QuotesSpacedArgument` |
| Basic syntax highlighting | Command/plain/quoted tokens render with different colors | static review: `CommandConsoleOverlay.DrawHighlightedInput`, `TokenColor`; Unity rendering is user-acceptance territory |
| Auto-scroll | Text area scrolls to newest line when line count changes | static review: `CommandConsoleOverlay.DrawTextArea`, `_lastLineCount` |
| Focus/mouse block/ESC in Unity | Focus enforcement, modal routing and ESC consumption are in the overlay/plugin path | static review: `CommandConsoleOverlay`, `Plugin.Update`, `OnlineUiOverlay`; Unity IMGUI behavior is user-acceptance territory |

## 3. Known remaining gaps

- No semantic highlighting beyond basic token colors (no per-argument error
  highlighting, no rich hover syntax details yet).
- No actual CUO command yet declares `Selector` or `Json` arguments, so the new
  argument providers exist at the seam but are not visible on a real command
  invocation; the tokenizer still preserves those literals.
- No IME support yet; printable ASCII/UTF insertion, cursors, word editing,
  selection and clipboard are covered.

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1903 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass (including GameState isolation, item authority, no-legacy, command authority, kernel shape) |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
| Protocol | unchanged |

## 5. Structure review

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
