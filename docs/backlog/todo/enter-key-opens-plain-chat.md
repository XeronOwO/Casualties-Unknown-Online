# Enter should open the console as plain chat

- Status: Todo — Rejected (batch `20261018-t`: row E2 **fails** — after the game's own console has been
  opened and closed, the event system still names its input as the selected object, so
  `OnlineUiKeyboardFacts.IsATextFieldFocused` stays true and the Enter/Slash opener is refused until
  something else takes the selection. The clean-state opener, the swallow, the submit, the chat delivery and
  both no-open surfaces pass; record
  `docs/evidence/acceptance/enter-key-opens-plain-chat-20261018-t.md`).
- Priority: Low
- Category: Online UI / input
- Source: the user's 2026-10-07 backlog request — pressing Enter should open the command/chat input the same
  way `/` does, but without the `/` prefix, so it is ordinary chat.
- Related: `src/CasualtiesUnknownOnline.Plugin/CommandConsoleOverlay.cs` (the `/` opener and the in-overlay
  Return handling), `done/in-game-command-console-interactive.md`,
  `done/command-console-esc-not-intercepted.md` (the same interception surface, ESC),
  `src/CasualtiesUnknownOnline.Runtime/Session/Chat/ChatService.cs` (the text-chat domain a plain line goes to)

## What is asked

Two openers, two entry modes, one input surface:

- `/` opens it with the command prefix, as today.
- Enter opens it in plain-chat mode: the field starts empty (no `/`), and Enter submits the line to the chat
  domain. A line that happens to begin with `/` is still a command, so the two openers differ only in what is
  pre-filled, never in what the surface can do.

## What is observed

- The overlay already opens on a pending focus plus `KeyCode.Slash`, and it already handles `Return` /
  `KeypadEnter` inside the open overlay (submit). The chat half exists: a non-command line is forwarded to the
  text-chat domain, and the console's own banner says "type /help for available commands, or just type to
  chat".

## What is not known yet

- Whether the game or the Online UI already consumes Enter in the states where the user wants this (in-world
  gameplay, an open container panel, the Online UI window, the death/spectator panels). The sibling ticket
  `done/command-console-esc-not-intercepted.md` exists because this interception surface is easy to get
  wrong in exactly that way, so the reading comes before the binding.
- Whether the opening keypress must be swallowed by the overlay (otherwise the same Enter key-down could reach
  the freshly opened field and submit an empty line).

## Required work

1. Attribute first: list every consumer of Enter/KeypadEnter in the local client (game input, Online UI
   fields, panels) and decide the precedence rule; the opener must not steal Enter from a text field the
   player is already typing in.
2. Add Enter as a second opener that opens in plain-chat mode, and make the entry mode a property of the open
   call rather than a second overlay state.
3. Make the open press un-ambiguous: the key-down that opened the overlay must not also submit the empty line.
4. Verify in the driver's own vocabulary plus a real session: `/help` still works, a plain line reaches the
   other members, Enter inside the overlay submits, and Enter does not open the console while the player is
   typing in another field.

## Non-goals

- Not a chat rework: the chat domain, its wire path and its bounded buffer stay as they are.
- Not a key-binding configuration surface (that can follow if the user wants one).
## Attribution and implementation (2026-10-17) — awaiting the run

### The Enter consumers, and the precedence rule (required work 1)

| Who reads Enter / KeypadEnter | Reachable in play? | What the opener does about it |
|---|---|---|
| CUO's own console (submit) | while it is open | not a consumer: the opener is gated on the console being closed |
| The Online UI window's own text fields | yes | gated on the window being hidden |
| The quick panel | yes | gated on the panel being hidden — it carries no text field, so that check is defensive |
| The quick-panel hotkey, configurable to ANY key | yes | `/` and Enter are claimed first, so a panel bound to Enter is shadowed — a limit, below |
| **The game's own in-world console** (it runs `Input.GetKeyDown(13)` with its field focused) | yes | asked through `INativeInputBlocker.IsTheGamesOwnTextInputFocused` |
| **The game's settings key-bind capture** (it binds whichever key the player presses) | yes | the same reading |
| **The game's crafting recipe filter** (the game suppresses its own hotkey there) | yes | the same reading |

The rule: CUO claims Enter only while no CUO surface is up AND the game's own keyboard focus is not a text field.
The last three are what an adversarial review added to this list before the commit — a first cut guarded only
CUO's own surfaces, which would have made an in-world console command submit twice and a key-bind capture open
chat over the pause menu.

### What landed

- **The entry mode is a property of the open call, not a second state**: `ConsoleInputSession.Open(bool plainChat)`
  pre-fills the command prefix only in command mode, and nothing else in that type touches the field's opening
  contents, so a close-then-reopen cannot carry a mode.
- **The opening key is swallowed**, whichever it was (numpad Enter counts as Return), so the Return that opened
  plain chat cannot also submit the empty line it just created.
- **Both new branches report themselves** at Information: the opener names the mode, the swallow names the key.
- **The game-side reading rides `INativeInputBlocker`** — the same seam the ESC suppression uses for the mirror
  problem — because the plugin references no game assembly.
- **The reading itself lives in its own type** (`OnlineUiKeyboardFacts`), and its truth is a fact about the game's
  event system's selected object.

### Limits this cycle names

- The quick-panel hotkey is configurable to Enter; the opener claims the key first, so a panel bound to Enter no
  longer toggles from a bare Enter while no CUO surface is up. Naming it rather than arbitrating it: the two are
  the same key and the console is the newer claim.
- The swallow rests on the pre-existing assumption that the opening key-down is still the current IMGUI event on
  the first drawn frame. The empty line cannot be submitted even if that misses — the session refuses an empty
  submit, and so do the command service and the chat policy — so the residual risk is a leaked Return, which is
  the game-side question above.
- A `/` and a numpad Enter in the SAME frame still reach the empty-command path (pre-existing, contrived).
- `GameAdapter` crossed the 600-line architecture gate by two lines with this cycle's port; it is recorded in
  `docs/architecture-debt.json` rather than split mid-cycle, next to the one entry that was already there.