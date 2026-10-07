# Enter should open the console as plain chat

- Status: Todo
- Priority: Low
- Category: Online UI / input
- Source: the user's 2026-10-07 backlog request — pressing Enter should open the command/chat input the same
  way `/` does, but without the `/` prefix, so it is ordinary chat.
- Related: `src/CasualtiesUnknownOnline.Plugin/CommandConsoleOverlay.cs` (the `/` opener and the in-overlay
  Return handling), `review/in-game-command-console-interactive.md`,
  `review/command-console-esc-not-intercepted.md` (the same interception surface, ESC),
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
  `review/command-console-esc-not-intercepted.md` exists because this interception surface is easy to get
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
