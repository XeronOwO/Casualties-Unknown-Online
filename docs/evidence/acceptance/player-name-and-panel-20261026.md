# A dead lobby's `player-0` and the panel layout — read from the machine (2026-10-26)

Two things were owed a frame: the Home page's host line after the zero-id fix (`401cbeb8`), and the interaction panel's row layout after the
width change. One frame judged both, on the deployed build.

`.acceptance/frames/panel-f6.png` (1280×720, captured on the host with the CUO panel open over a live paradise world):

- **the host line shows the player's own Steam name**, where the same line read `player-0` in the frame that reported the defect. A zero id can no
  longer reach the name renderer (`OnlineUiContext.DisplayName(0)` answers the keyed `ui.unknown_player`), and this is what a live session shows
  instead;
- **the session block is honest**: the member count is the live one (`3`), the lobby id is the one the client is actually in, and the build line reads
  `0.1.0+705821d` — the artifact identity the deploy verified, visible on the player's own screen;
- **the quick panel's rows are aligned**: one target line, its candidate rows one per line, then the kick and ban buttons on their own row, with the
  state strip under them. That is the layout the `RowWidth = Width - 12` change was for; nothing overlaps and no name is glued to a button.

What this note does **not** claim: that the lobby-loss path itself was re-tested (the session was healthy when the frame was taken), and nothing
about the panel's behaviour — only that its rows render as designed.
