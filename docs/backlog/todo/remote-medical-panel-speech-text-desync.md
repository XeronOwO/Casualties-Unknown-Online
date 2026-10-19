# The speech shown while a remote medical panel is open is not synchronized

- Status: Todo
- Priority: High
- Category: Remote medical / WoundView display / character speech bubble
- Source: User report (2026-10-19, during acceptance batch `20261023-b`): "角色说的话，会在医疗面板以文字形式显示，这个不同步" — while viewing another player's medical panel, the character's spoken line shown at the top centre of the screen is not the displayed player's line.
- Related: `review/sync-player-pain-vocalizations-and-bark.md` (the audio sibling), `review/remote-player-medical-panel.md`, `todo/remote-medical-panel-header-shows-viewer-identity.md`, `todo/remote-medical-panel-alarm-follows-viewed-player.md`

## What the text is

The line at the top centre is NOT a `WoundView` text. It is the native `Talker` world
bubble: `Talker.cs:380-414` types `currentString` into a world-space `TextMeshPro`
prefab above the speaker, and the panel's own labels are the static `nameText`
("EXPERIMENT", no writer in Assembly-CSharp) and the identity line (its own ticket).
The speech domain already exists in CUO:

- `TalkerPatch` suppresses a remote clone's own talk rolls and reports the local body's
  line once (`PatchBridge.OnSpeechReported`).
- `SpeechSync` reports a guest's bubble to the host / broadcasts the host's, and the
  receiving side replays it on the speaker's clone: `GameObject.Find("Character_<steamId>")`
  → `Talker.currentString = text`, `timeSinceTalked = 0`, inside the `RemoteApply` scope
  (`SpeechSync.ReplayOnClone`).

So the expected behaviour is already designed — every screen shows the speaker's line,
typed out by that clone's own `Talker.Update`. The run observed a mismatch while a remote
medical panel was open, and this ticket is to find which half of that path breaks.

## What is known / what is not

- Known: the reported text is the world bubble, not a panel label (user clarification
  2026-10-19); the panel's top-centre area is where the bubble lands because the camera
  centres the character.
- Not known (verify before touching code): whether the visible line is the viewer's OWN
  body's bubble, a stale line from the display body's parked copy, a replay that never
  reaches the clone, or a replay that lands but is not rendered under the panel's camera.
  Name the witness for each: the Talker owner body, its `currentString`, the speaker's
  own `currentString`, and the clone's `currentString` on each client.

## Reproduction / acceptance matrix

Reproduction: two clients in one world; the guest opens the host's medical panel. The host
speaks through the game's own line (B-key bark or a driven `Talker.Talk`), then the guest
speaks; read each client's talker facts and the visible bubble.

| # | Scenario | Expected |
|---|---|---|
| 1 | Host speaks while the guest views its panel | The guest's screen shows the host's line, the same string the host's own screen types |
| 2 | Guest speaks while the host views the guest's panel | The host shows the guest's line (the reverse direction) |
| 3 | Third-party view | The bystander shows the speaker's line |
| 4 | Panel closed | The same lines as row 1-2; the panel must not change the speech path |
| 5 | Viewer's own body speaks while viewing a peer | The viewer's own bubble is not presented as the peer's; each line is attributed to its own speaker |
| 6 | No echo / no double | A replayed line is not re-reported; one line, one bubble per screen |

## Non-goals

- Voice audio (the pain/bark sound family is `sync-player-pain-vocalizations-and-bark.md`).
- A new speech UI; the world bubble stays the only surface.
