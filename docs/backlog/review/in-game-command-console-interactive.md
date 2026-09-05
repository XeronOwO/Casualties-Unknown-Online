# Interactive in-game command console

- Status: Review (code complete; waiting for the final unified acceptance pass)
- Priority: High
- Category: Tooling / UI / Minecraft-style command console
- Source: User rejection (2026-09-05) — the whole feature was rejected and has been redone in this cycle.

## Rejected as a whole — user feedback

The current implementation does not match the Minecraft command/chat
experience the user expected, and the feature must be redone from the UI
perspective. Specific rejection points:

- The console panel is too large and not transparent. It should be a Minecraft-like
  translucent, compact overlay anchored at the bottom of the screen.
- The input line has layout/alignment problems: the `>` prompt and the input box
  are misaligned.
- The header/help text ("回车: 发送; Tab: 补全; 1/1: 历史; ESC: 关闭") should be
  removed. Users who use commands do not need these instructions.
- Pressing `/` did not pop up the command suggestion list / autocomplete in the
  way Minecraft does. The user expects Minecraft-style command suggestions and
  completion (the command tree exists, but the visible interactive experience is
  missing or not working as expected).
- History/fade behavior is wrong:
  - While the command panel is open, all command/console history should be shown
    (no fading).
  - Fading should only be used for non-open-panel notifications, to tell the
    user that something happened.
- Pressing ESC to close the command panel must be fully intercepted. Currently it
  also triggers the game's ESC/pause menu, which means the modal input guard does
  not swallow the key on the game's own input path.
- The user explicitly provided Minecraft server and reverse-engineering resources
  for this purpose. The redo must study the Minecraft command/chat UI rather than
  inventing a new design.

## Redo completed (2026-09-05)

- Replaced the large opaque modal panel with a compact, translucent,
  bottom-anchored console overlay; the world remains visible behind history.
- Removed the title/header and the key-instruction line; the input prompt and
  field are aligned in one manual input row.
- Added live Minecraft-style suggestions: `ConsoleInputSession.LiveSuggestions`
  always reflects the current input, so pressing `/` immediately shows command
  candidates and typing narrows them without requiring Tab.
- While the console is open the full command/chat history renders with no fade;
  `ConsoleFadePolicy` is now used only for the small closed-panel notification
  strip that shows the most recent lines for a short time.
- ESC handling is present in the overlay, but the user re-tested and reports it
  is still not fully intercepted (see the open issue below).
- Input presentation (caret, selection, highlighting, IME) was split into
  `CommandConsoleInputRenderer`, keeping the overlay under the architecture line
  gate and preserving the single-responsibility split.
- Added `LiveSuggestions` regression tests (open/closed behavior).

Expand the initial modal Online UI console into a standalone interactive
in-game command console: `/` opens a focused input box, text lines fade,
completion/history/hints are available, and the input state blocks other game
interaction. The local command execution chain and text-chat forwarding stay in
Runtime; the new interaction model is a first-class UI surface that does not
require opening the Online UI window. The selector argument provider now also
feeds a real command: `/heal <selector>` resolves player selectors and sends a
host-authoritative heal request. The custom input is IME-aware: it enables
Unity's legacy IME while the console is open, tracks composition in a
Unity-free state, and renders the composition string at the caret. The JSON
argument provider also feeds a real host-only command: `/hostrules <json>`
parses a flat JSON object and persists host/respawn settings. Completion now
uses a command-tree/argument-node model with resource-location candidates, and
selectors support bracketed filters (`type`, `name`, `distance`, `limit`,
`sort`).

## ESC interception resolved

The previous ESC re-report is now closed by a one-frame modal suppression:
`Plugin.Update` keeps `SetOnlineUiModal(true)` for the first frame after any
CUO ESC-closing surface (standalone console, Online UI window, quick panel)
closes, and a non-modal escape-surface guard suppresses the pause toggle while
the quick panel is open, so the closing ESC cannot be seen by the game's native
pause input in the same frame. The dedicated ticket is
`docs/backlog/review/command-console-esc-not-intercepted.md`.

Phase plan: `docs/phases/command-console-interactive/README.md`.
Selfcheck: `docs/evidence/selfchecks/ui/command-console-selfcheck.md`.
