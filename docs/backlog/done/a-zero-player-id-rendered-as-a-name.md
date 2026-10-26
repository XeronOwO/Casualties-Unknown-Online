# A zero player id rendered as a player named `player-0`

- Status: Done — fixed 2026-10-26 (`401cbeb8`), judged by a frame the same day.
- Priority: High — a player-visible line, and it named a session that no longer existed.
- Category: Online UI / Home page / name resolution

## What the player saw

The Home page's session block read its host field as `player-0` and a member count of `0` after both members had left, over a lobby id that
Steam no longer knew. The player's own Steam name and id above it were correct, so the session block was the only wrong part.

## The cause, in two facts

1. `SteamService.CurrentLobbyId` keeps the last lobby id after Steam drops the link, while `SteamService.GetLobbyOwner()` answers `0` for a lobby it
   no longer knows. `Plugin/OnlineUiHomeDrawer.cs` drew the whole block from the stale id, so its host line resolved to id 0.
2. `Plugin/OnlineUiContext.DisplayName(0)` printed `player-0`: a zero id is not a player, and that string reads like a real person's name rather than
   like "this lobby is gone".

## The fix

- `OnlineUiContext.DisplayName` answers the keyed `ui.unknown_player` for id 0 — the one funnel every name render goes through, so the guard covers
  the family rather than the one line;
- `OnlineUiHomeDrawer` draws the lobby block only when the owner query proves the lobby is still real (`lobbyOwner != 0`), and otherwise says
  `home.lobby_none`.

## Judged

`docs/evidence/acceptance/player-name-and-panel-20261026.md`: on the deployed build the host line shows the player's own Steam name, the member count
is live, and the panel's rows render as designed.

## Limits

- The frame was taken in a healthy session; the lobby-loss path itself is not re-triggered by it. That path's own reading stays what the fix's
  reasoning covers: an owner query of 0 is treated as "no lobby".
