# The Online UI overlay's layout, read on the live session (2026-11-08)

## Fixture

- Build `0.1.0+75133718` (acceptance channel), batch `20261108-z` (lobby `109775243869416010`, host at 18590).
- The overlay open at its default size (frame `plugin-host-shell.png`).

## Instrument

- `tools/acceptance/recipes/cuo-ui-dump.cs`: one entry per `RectTransform` under the CUO Online UI window — path, rect, active flag, span (its box in the window's own units), LayoutElement, Image, TMP text, Button, Toggle, InputField, Slider.
- `capture-frame` + a crop of the window (`plugin-host-shell-window.png`).

## Readings

- The window's own rect `1000.00x700.00`, span `-500.00..500.00 x -350.00..350.00`; on the 1280×720 screen at uiScale 0.66 that is 660×462 px, fully inside the screen.
- 134 `RectTransform`s under the window; **0** has a span outside the window's span (nothing is clipped).
- 10 interactive controls, every one a Button with `interactable` true; no Toggle, InputField or Slider on this page (the page's controls are buttons and labels).
- The labels' text, read from the dump: the title carries the mod's name, the six tabs read home, players, network, admin, saves and preferences, the two section headings read Steam status and session, the session lines carry the initialized flag, the nickname, the SteamID, the role/handshake line, the lobby id, the host line, the member count, the RTT and the build line (`0.1.0+7513371`), the in-session notice is present, and the three buttons read copy the lobby ID, close the room and open the player page.
- No image with a colour alpha of 0 (no hidden prefab art left as an empty control).
- The frame crop shows the whole window: the game's own dark panel, the cyan labels, the green status text, the section headings in yellow, and the scroll content; nothing is cut at the window's edge.

## What this settles

The layout half of "the overlay's look": it fits the screen, nothing is clipped, every control exists and is enabled, and every label carries its text. The frame crop is the visual companion.

## Limits

- The taste half — colours, typography, tone — is a product decision, not a machine row; the ticket's acceptance names it as the user's judgement and it stays that.
- One observation, not a defect: the build label renders the short identity `0.1.0+7513371` (seven hex characters), not the full SHA.
