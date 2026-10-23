# Enter should open the console as plain chat

- Status: Review — the guard fixed 2026-10-24 (the active-in-hierarchy reading); awaiting the green
  re-run of row E2 on the fixed artifact. The earlier rejection (batch `20261018-t`: row E2 **fails** — after
  the game's own console has been opened and closed, the event system still names its input as the selected object, so
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
| **The game's own in-world console** (it executes Enter whenever the console is `active`, with no focus test) | yes | asked through `INativeInputBlocker.IsTheGamesOwnTextInputFocused`; the reading is the console's own `active` flag, so it holds even if the selection moves while the console stays open |
| **The game's settings key-bind capture** (it binds whichever key the player presses) | yes | **NOT covered** — it is a button-driven coroutine with no text field, and no capture-state fact is readable; filed as `todo/enter-opener-keybind-capture.md` (the pause-wide gate it would need changes when the opener works, so it is a decision) |
| **The game's crafting recipe filter** (the game suppresses its own hotkey there) | yes | the same reading (an active selected text field) |

The rule: CUO claims Enter only while no CUO surface is up AND the game does not own the keyboard (its console
is open, or an active selected text field has the keys). The last three are what an adversarial review added to
this list before the commit — a first cut guarded only CUO's own surfaces, which would have made an in-world
console command submit twice. The settings key-bind capture is the one row the reading cannot see, and it has
its own ticket; this ticket's Limits state it rather than claiming coverage.

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

## The guard fixed (2026-10-24) — the lifetime reading and the console's own flag

`OnlineUiKeyboardFacts` now answers "the game owns the keyboard" from two facts, and its method name says so
(`IsTheGameUsingTheKeyboard`):

- the game's in-world console is OPEN, read from `ConsoleScript.instance.active` — the console's Enter path has
  no focus test, so its field holding the selection is not what makes it dangerous (the review of this fix found
  the selection can move while the console stays open, a pause menu or a click on a non-selectable area);
- OR the event system's selected object is an ACTIVE text field (`activeInHierarchy` plus one of the two input
  components), which is the lifetime half: the console selects its input when it opens and never clears the
  selection when it closes, so the event system keeps naming a field whose canvas is inactive — an object that
  cannot receive a key. Asking only "does the selected object carry a text field" kept the CUO Enter/Slash
  opener refused after one use of the game console.

The red was observed on the deployed pre-fix artifact `0.1.0+2dfe2071…` with a two-client world
(`.acceptance/20261024-c/enter-red2.log`): baseline `guard=false`; the game console opened through its
`console` bind (`active=true`, `selected=Console`, `selectedActive=true`, `guard=true`); closed
(`active=false`, `selected=Console`, **`selectedActive=false`**, `guard=true`); after Return `guard=true` and no
CUO console in the frame (`enter-red2-after-enter.png`). The green repeats the same steps on the fixed artifact.

Two committed probe recipes carry it, because no unit host can build the game console or the event system:
`game-console-focus` reads the console's flag, the selection, its active state, the selected object's two input
components and the guard (through reflection, the guard being adapter-internal; a guard it cannot find is an
error, never a false green), and `key-tap` posts a raw virtual key through the `window-key` declaration; the
game's own console is toggled with the existing `key-hold bind=console`.

### Limits this fix names

- **The game's settings key-bind capture is not covered.** It is a button-driven coroutine that binds whichever
  key is pressed and carries no text field; the pause-wide gate it would need changes when the player can open
  CUO's console, so it is a decision and is filed as `todo/enter-opener-keybind-capture.md`. The guard's own doc
  comment names the gap.
- **The slide-out window is dead, not double-used.** After the console's `active` flag flips false, its canvas
  stays active for ~0.29 s (the console rect has to cross 150 px), so the guard stays true and Enter is ignored
  for that moment; the game ignores Enter in the same window (`if (!this.active) return`). The opening window is
  about one frame.
- **A disabled text field that keeps the selection still answers true.** A field that cannot take keys over-blocks
  the opener; no game surface was observed doing that, and the alternative (reading `isFocused` per component)
  would miss the console's stale field, which is the defect this fix exists for.