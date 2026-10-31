# Escape closing the console also opens the game's pause menu

- Status: Done — fixed 2026-11-01, all three rows judged on the deployed build.
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
| 1 | Escape with the console open closes the console and does NOT open the pause menu — one frame, one press | **PASS 2026-11-01**: real `/` then Escape on the deployed build; frame `esc-after-fix.png` shows the world with no console and no pause menu, where the pre-fix batch's `console-key-closed.png` showed the pause wheel behind the closed console |
| 2 | Escape with the console closed still opens the pause menu | **PASS 2026-11-01**: frame `esc-pause.png` — the game's own pause screen with its wheel, so the guard suppresses only the console's press |
| 3 | A movement key pressed while the console is open does not move the player — read from the body's position across the press | **PASS 2026-11-01**: the body read `(0, 488.615)` before and after a held `W` (VK 87, 0.7 s) with the console open — identical, so the line was consuming the keys |

## Limits

- The batch that found this had one client and no session; the leak is a local input question and does not need two.

## The fix and the reading (2026-11-01)

The guard existed and simply did not include the console: `OnlineUiHost` set the "a non-modal ESC-closing CUO surface is open" fact from the quick panel's visibility alone, so while the console was open the fact read false and the game's `TogglePause` ran on the same Escape. The console's open state now joins that fact (`quickPanelVisible || consoleOpen`), which is the rule the Online UI window and the quick panel already followed. Verified by three frames and one body read on the deployed build: the console closes without the pause menu, Escape with the console closed still opens it, and a held movement key with the console open moves the body by nothing.