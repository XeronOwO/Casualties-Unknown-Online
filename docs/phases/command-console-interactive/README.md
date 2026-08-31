# Interactive In-Game Command Console — Phase Plan

This directory tracks the phased implementation of the interactive command
console. It is a working document: each phase notes what was delivered, what
evidence was produced, and what remains.

## Target behavior

1. The console is split into a text area and an input box.
2. Pressing `/` directly in game opens the input box without opening the
   full Online UI window.
3. New console lines appear in the text area and fade after a configured hold
   time.
4. The input box keeps focus, has usage hints, Tab completion, Up/Down history,
   and best-effort completion for quoted/spaced values, selectors, and JSON-like
   literals.
5. While the console input is open it blocks other mouse/keyboard game
   interaction; Escape exits the input state.
6. Valuable console interaction patterns (history, completion, usage
   hints, fading chat) are included; no external game source is committed.

## Current implementation baseline

The first slice landed a modal Online UI console page:

- `CommandConsoleService` owns command registration, slash parsing, role
  permission, execution, and a bounded output buffer.
- `OnlineUiConsoleDrawer` renders the console page inside the modal Online UI
  window.
- Commands: `/help`, `/clear`, `/players`, `/rtt`, `/whoami`, `/kick`, `/ban`,
  `/unban`; plain text goes through `ChatService`.
- Tests: `CommandConsoleServiceTests`.
- Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.

## Status

| Phase | Status | Evidence |
|---|---|---|
| 1 — Recon/plan | Complete | This plan plus the existing input-chain source (`Plugin.Update`, `OnlineMenuInputGuard`, `PlayerCameraHandleInputPatch`). |
| 2 — Runtime model | Complete | `cc8224e`; new completion/tokenizer/input-session/fade types, focused tests. |
| 3 — Standalone overlay | Complete | `003492e`; slash hotkey, modal routing, focus/ESC handling. |
| 4 — Fading + hints | Complete | Overlay and Online UI console page render age-based alpha; hint/completion rows are shown. |
| 5 — Verification/docs | Complete | Full build/test/gates pass; selfcheck and backlog updated in the final docs commit. |
| 6 — Rich hints/suggestions | Complete | `CommandSuggestion` metadata, clickable suggestion rows, hover tooltips, `/help <command>` usage. |
| 7 — Cursor-aware input/editing | Complete | Custom IMGUI input with caret, arrows/Home/End, Backspace/Delete, and Tab completion replaces the token at the cursor. |
| 8 — Basic syntax highlighting | Complete | Command tokens rendered in accent color, quoted/bracket/brace literals in muted color, plain text in default color. |
| 9 — Word editing | Complete | Ctrl+Backspace/Ctrl+Delete delete words; Ctrl+Left/Right move by word. |
| 10 — Auto-scroll | Complete | Text area jumps to the newest line when the console line count changes. |

## Phases

### Phase 1 — Recon and input-chain evidence

- Document the existing IMGUI/modal chain: `Plugin.Update` →
  `IGameAdapter.SetOnlineUiModal` → `OnlineMenuInputGuard` →
  `PlayerCameraHandleInputPatch` / `PauseHandlerTogglePausePatch`.
- Decide where the slash hotkey and the standalone overlay hook into that chain.
- Outcome: this README plus the relevant architecture notes; no production code.

### Phase 2 — Runtime command metadata, completion, history model

- Add immutable command metadata (name, description, usage, permission,
  argument kinds).
- Add a command completion source that is testable without Unity.
- Add a `ConsoleInputSession` that owns open/close, history navigation,
  completion cycling, and line submission.
- Add a command-line tokenizer that preserves quoted, selector, and JSON-like
  literals.
- Add a fade policy primitive for UI line aging.
- Outcome: pure Runtime behavior covered by unit tests.

### Phase 3 — Standalone modal overlay

- Add a `CommandConsoleOverlay` IMGUI surface that draws only when the input
  session is open.
- Wire the `/` hotkey in `Plugin.Update`.
- Route the existing modal blocker so the overlay blocks background UI and game
  input.
- Keep the text field focused and handle Enter/Tab/Up/Down/Escape in the
  overlay.
- Outcome: in-game `/` opens a focused, blocked-input console.

### Phase 4 — Fading text area and polish

- Render console lines with age-based alpha using the fade policy.
- Show usage hints and completion candidates under the input box.
- Keep history/completion behavior consistent across the overlay and the
  existing Online UI console page where practical.
- Outcome: user-visible console text fade and completion UI.

### Phase 5 — Verification, backlog, selfcheck

- Add/expand tests for core and edge paths.
- Run all required build/gates.
- Update `docs/backlog/README.md`, the backlog ticket, and the console selfcheck.
- Commit each completed phase separately, with GPG signatures.

### Phase 6 — Rich hints/suggestions

- Add `CommandSuggestion` metadata so each candidate carries a description.
- Render suggestions as a scrollable, clickable list with hover tooltips.
- Add `/help <command>` specific usage output and command-name completion for
  the help argument.
- Remaining high-end gaps are tracked in the selfcheck and are not presented as
  complete Minecraft parity (no entity-selector / resource-location argument
  trees).

### Phase 7 — Cursor-aware input/editing

- Replace the native Unity `TextField` with a custom IMGUI input that owns the
  cursor in `ConsoleInputSession`.
- Support mouse-click cursor placement, Left/Right/Home/End, Backspace/Delete,
  and printable character insertion.
- Tab completion now replaces the token under the actual cursor, preserving text
  after the token.
- Outcome: Minecraft-style in-place editing and completion.

### Phase 8 — Basic syntax highlighting

- Render the custom input with per-token colors: command names in accent,
  quoted/selector/JSON-like literals in muted, plain text in default color.
- Outcome: command text is visually distinguishable while typing.

### Phase 9 — Word editing

- Add Ctrl+Backspace/Ctrl+Delete word deletion and Ctrl+Left/Right word jumps
  in the cursor-aware input.
- Outcome: faster command/chat editing without relying on native TextField
  shortcuts.

### Phase 10 — Auto-scroll

- Track the console line count and reset the text-area scroll to the bottom when
  new lines arrive.
- Outcome: new chat/command output is visible without manual scrolling.

## Non-goals

- No external game code, assets, or reverse-engineered source is committed.
- No new wire protocol or packet handler.
- No host/guest command relay; the console remains a local surface.
