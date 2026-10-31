# Escape closing the console also opens the game's pause menu

- Status: Todo
- Priority: Low
- Category: Console / input behaviour
- Source: the 2026-11-01 acceptance batch that judged the console's key toggle (frames `console-key-open.png`, `console-key-closed.png`).

## The reading

With the CUO console open, one Escape press closes the console — and the game's own pause menu comes up behind it in the same
frame. The close frame shows the game's pause screen ("hard to look at", the wheel with the restart/settings/skull buttons) where
the world should be.

## Why

The console's key handler consumes the IMGUI event (`CommandConsoleOverlay.HandleKey` calls `evt.Use()` in its Escape branch), and
that is all `evt.Use()` can do: it stops the IMGUI pass from acting on the event. The game reads its own keys through
`Input.GetKeyDown`, a different channel that a consumed IMGUI event does not touch, so the same physical press reaches both
surfaces.

## Design

The console surface and its keys are described in [`docs/design/mechanics/console.md`](../../design/mechanics/console.md): the
overlay owns the line and its keys while it is open. A key the overlay consumed has to be a key the game does not also see, or the
"one press = one meaning" rule the overlay's own comments cite is broken for the one key every player will press.

## What a fix has to decide

1. **Where the swallow belongs.** Either the overlay's Escape is also removed from the game's own read while the overlay is open (a
   patch that reports the key as not pressed for the frame the overlay consumed it in), or the overlay closes on a key the game
   does not bind at all (a design change with its own cost: Escape is what every player tries).
2. **The other keys with the same shape.** The same leak applies to every key the overlay handles and the game also binds — the
   arrow keys and the movement keys while the line is being edited. The fix is a rule for the overlay's whole key set, not one
   branch for Escape.
3. **What the game's pause menu does after the console closes on purpose.** If the escape is swallowed for the console, the player
   needs a second press to pause — which is the intent (the console was the surface being closed) and should be stated where the
   console's keys are documented.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Escape with the console open closes the console and does NOT open the pause menu — one frame, one press | unjudged |
| 2 | Escape with the console closed still opens the pause menu | unjudged |
| 3 | A movement key pressed while the console is open does not move the player — read from the body's position across the press | unjudged |

## Limits

- The batch that found this had one client and no session; the leak is a local input question and does not need two.
