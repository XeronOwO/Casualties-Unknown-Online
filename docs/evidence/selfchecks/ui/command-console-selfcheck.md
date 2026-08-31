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
| 16 | Undo/redo | `ConsoleInputSession` keeps bounded undo/redo stacks; Ctrl+Z/Ctrl+Y restore editing state. |
| 17 | No wire change | No `NetMsg`, no packet handler, no `ProtocolVersion` change. |
| 18 | Real selector command | `/heal <selector>` is the first real CUO command with a `Selector` argument; `CommandSelectorResolver` expands player selectors over `IEntitySyncControl` and the resolved SteamIds ride the existing heal request path. |
| 19 | IME composition | `ConsoleImeState` is a Unity-free composition gate; the overlay enables legacy IME while open, feeds `Input.compositionCursorPos` from the caret, renders the composition string, and swallows raw keys during composition. |
| 20 | Real JSON host-rule command | `/hostrules <json>` is the first real CUO command with a `Json` argument; `HostRulesJsonParser`/`HostRulesJsonApplier` parse and apply a flat JSON object through the narrow `IHostRulesEditor` seam. |

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
| Real selector completion | `/heal @` suggests selector candidates on a real command | `CommandConsoleServiceTests.Suggest_ForHealSelectorArgument_ReturnsSelectors` |
| Real selector heal | `/heal @p` resolves the nearest remote and runs the host heal path | `CommandConsoleServiceTests.Heal_WithSelector_SendsRequestToGuest` |
| Selector resolver core/edge | `@a`/`@e`/`@s`/`@p`/`@r`, unknown, empty, case-insensitive, no-remote paths | `CommandSelectorResolverTests` (10 cases) |
| Heal argument validation | Missing selector shows usage; unknown selector adds a no-match line | `CommandConsoleServiceTests.Heal_WithoutSelector_ShowsUsage`, `Heal_UnknownSelector_AddsNoMatchLine` |
| IME composition gate | Initial/active/empty/null/clear composition states are deterministic | `ConsoleImeStateTests` (5 cases) |
| IME overlay wiring | IME mode toggled on open/close, composition rendered, raw keys swallowed while composing, caret position fed to Input | static review: `CommandConsoleOverlay.Open/Close/HandleKeys/DrawImeComposition/UpdateImeCursorPosition`; OS IME behavior is user-acceptance territory |
| Real JSON completion | `/hostrules {` suggests a JSON object template on a real command | `CommandConsoleServiceTests.Suggest_ForHostRulesJsonArgument_ReturnsTemplates` |
| Real JSON host-rule update | `/hostrules {...}` parses and applies through the editor seam | `CommandConsoleServiceTests.HostRules_WithJson_UpdatesEditor` |
| JSON parser core/edge | Flat object, empty object, case-insensitive keys, quoted values, malformed/trailing/empty input | `HostRulesJsonParserTests` (8 cases) |
| JSON applier | Valid object applies pairs; editor rejection and malformed input return errors | `HostRulesJsonApplierTests` (3 cases) |
| Host-rule command validation | Missing JSON shows usage; malformed JSON adds an error line | `CommandConsoleServiceTests.HostRules_WithoutJson_ShowsUsage`, `HostRules_MalformedJson_AddsError` |
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
| Undo/redo | Ctrl+Z/Ctrl+Y revert/reapply typing, deletion and completion | `ConsoleInputSessionTests.InsertChar_ThenUndo_RestoresPreviousInput`, `Undo_ThenRedo_ReappliesChange`, `Completion_ThenUndo_RestoresBeforeCompletion` |
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
- Selector expansion is player-entity only (`@a`/`@e` are aliases over remote
  players); generic entity/resource-location completion is still future work.
- The JSON command parser is intentionally flat-object only; generic nested
  JSON arguments, JSON arrays, and command-tree/resource-location completion
  remain future work.
- IME composition is wired through Unity's legacy Input Manager and the custom
  caret/render path; per-IME (Chinese/Japanese/Korean, platform-specific)
  acceptance is user-acceptance territory. Raw key suppression is intentionally
  conservative while composing.

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1942 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass (including GameState isolation, item authority, no-legacy, command authority, kernel shape) |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
| Protocol | unchanged |

## 5. Structure review

- `CommandConsoleService` remains under the line-count gate and now implements
  both `ICommandControl` and `ICommandCompletionSource`; the completion data is
  still owned by the command registry. It composes the narrow
  `IPlayerInteractionControl`/`IEntitySyncControl` seams for the real selector
  command while keeping command/history policy out of the Unity overlay.
- `CommandSelectorResolver` is a pure static resolver with no mutable simulator
  state; selector semantics are explicit and covered by core/edge tests.
- `HostRulesJsonParser`/`HostRulesJsonApplier` keep JSON parsing and apply logic
  out of `CommandConsoleService`; `IHostRulesEditor` is the narrow write seam and
  the plugin supplies the real ConfigEntry-backed implementation.
- `ConsoleImeState` is a small Unity-free state object; the overlay only polls
  Unity's legacy Input APIs and renders the result, so IME policy stays testable.
- `ConsoleInputSession` is one top-level type, Unity-free, and owns all
  interactive input state; the Unity overlay is a thin presenter.
- `CommandConsoleOverlay` is one top-level type and owns only IMGUI drawing/event
  translation; it does not own command/history policy.
- `CommandLineTokenizer` and `ConsoleFadePolicy` are pure static helpers with no
  mutable state.
- No new booleans exceeding the architecture gate; no dead mechanisms left behind.
