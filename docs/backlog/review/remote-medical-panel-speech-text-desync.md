# The speech shown while a remote medical panel is open is not synchronized

- Status: Review — code-complete; batch `20261023-b` passes rows 1-5 and 7-8 on the deployed artifact, row 6 (trade menu) stays `unproven` (no trader fixture); record `docs/evidence/acceptance/remote-medical-panel-speech-text-desync-20261023-b.md`
- Priority: High
- Category: Remote medical / native HUD / character speech subtitle
- Source: User report (2026-10-19, during acceptance batch `20261023-b`), translated from the reporter's Chinese: "what a character says shows as text on the medical panel, and it is out of sync"; clarified by the user: what appears is the VIEWER's own spoken line, it shows only while the medical panel is open, and it disappears when the panel is closed.
- Related: `review/sync-player-pain-vocalizations-and-bark.md` (the audio sibling), `done/remote-player-medical-panel.md`, `done/remote-medical-panel-header-shows-viewer-identity.md`, `done/remote-medical-panel-alarm-follows-viewed-player.md`

## What the text is (final diagnosis, 2026-10-19)

The user's sentence is the game's **screen-space speech subtitle**, not a `WoundView` text and not
the world bubble:

- `PlayerCamera.HandleOnscreenCharacterTalkText` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:3065-3077`)
  copies the LOCAL body's talker text into `charTalkText` and sets
  `charTalkText.enabled = this.woundView.activeSelf || this.tradeMenu.activeSelf`.
- The object is `Main Camera/Canvas/TalkText` (a `TextMeshProUGUI` sibling of the canvas, not a
  child of `WoundView`). That is why it shows exactly while a medical/trade panel is open and
  disappears when the panel closes — the user's own observation.
- The WoundView panel itself holds 48 texts and none carries a speech line; the world bubble
  (`Experiment/TalkText`, and the clone's bubble) is a separate copy that the speech sync already
  maintains.

The defect: `charTalkText` is fed from `this.body` — the viewer's own body — so while the viewer
looks at another player's panel, the viewer's own line is shown as if it belonged to the viewed
player. The world clone's replayed line (the viewed player's actual words) is not what the
subtitle reads.

## Fix (2026-10-19)

`RemoteMedicalTalkTextPatch` (a Postfix on `PlayerCamera.HandleOnscreenCharacterTalkText`, in
`RemoteMedicalPatches`) takes the subtitle over while `RemoteMedicalView.IsOpen`: it writes the
**viewed player's world-clone talker** text/color (the clone `SpeechSync` replays into; the
`MedicalDisplay_` copy is inactive and never fed), clears the line when the viewed player is
silent, and restores the LOCAL body's line on the first frame after the focus ends. The restore is
required because the native method only fades a silent local line; without it the peer's last line
would stick on the viewer's next local panel or trade menu. The native `enabled` state is left
untouched.

The takeover/restore rule is a Unity-free state helper (`RemoteMedicalTalkTextState`) with a unit
test; the visible typing and color need the live two-client run below.

## Reproduction / acceptance matrix

Reproduction: two clients in one world; the guest opens the host's native medical panel. Each side
speaks through the game's own line (B-key bark or a driven `Talker.Talk`); read
`PlayerCamera.charTalkText` (enabled, text, color) on both clients beside the speaker's own line.

| # | Scenario | Expected |
|---|---|---|
| 1 | Host speaks while the guest views its panel | The guest's subtitle shows the host's line, typed and colored from the host's clone, not the guest's line |
| 2 | Guest speaks while the host views the guest's panel | The host's subtitle shows the guest's line (the reverse direction) |
| 3 | The viewed player is silent | The subtitle is empty, not the viewer's own last line |
| 4 | The viewer speaks while viewing a peer | The subtitle does NOT show the viewer's line; it keeps the peer's current line or stays empty |
| 5 | Close the remote panel, then open the LOCAL panel | The subtitle shows the local body's own line (or nothing when silent); the peer's last line never appears |
| 6 | Trade menu open, remote panel opened over it | The same takeover/restore rules; no peer line on the trade menu's character line after close |
| 7 | Target leaves the world while the panel is open | The coordinator closes the focus; the subtitle restores the local line and never keeps the peer's line |
| 8 | Panel closed entirely | `charTalkText.enabled` is false; nothing is drawn |

## Batch `20261025-m` — the trade-menu fixture exists, and the production entry REFUSED the pairing

Row 6 could not be staged before because no probe opened the trade menu. It can now: `tools/acceptance/recipes/trade-open.cs`
calls the game's own entry — the nearest non-hostile `TraderScript` becomes `PlayerCamera.main.currentTrader` and
`TraderScript.OnUse()` toggles the menu, which is the exact call the player's interact makes. On the deployed build it answered
`menuActive=true`, `traderCount=8`, nearest trader at `(6,147)`, build health `1250`.

With the trade menu open, the same batch asked the PRODUCTION medical entry to open the victim's chart over it
(`medical-open steamId=76561199526807662`) and it answered **`opened=false, nativeOpen=false`** — the pairing row 6 describes
never happens, because the medical entry refuses while another native menu holds the screen. The frame at that moment shows the
trade HUD without the chart.

So row 6's premise needs a decision this batch cannot take: **should the remote chart open OVER the trade menu (the row's
assumption), or is refusing while trading the intended behaviour?** Everything the row would then assert (the takeover/restore
pass, no peer line on the trade menu's character line) follows from that answer. Recorded as the row's blocker rather than
judged — the code is consistent, the expectation is what is unstated.

## Non-goals

- Voice audio (the pain/bark sound family is `sync-player-pain-vocalizations-and-bark.md`).
- A new speech UI; the native subtitle and the world bubble stay the only surfaces.

## The speech line can be read directly now (2026-11-01, 07:25)

A new read-only recipe (`talk-text-read`) reads `PlayerCamera.charTalkText` — its `enabled`, its `text` and its colour alpha — together with `RemoteMedicalView.IsOpen`, so the takeover/restore rules can be judged as state rather than inferred. Two readings from the deployed build:

- with nothing open: **`enabled=false, text=(empty), medicalOpen=false`** — the row-8 condition (the "panel closed entirely" condition: the line is disabled and empty);
- with the trade menu open (`trade-open mode=open` answering `menuActive=true`) the line still read `enabled=false` at that moment, and `medical-open` answered opened=false in that state — and also after the menu was closed, because the viewed player was out of range for a render clone. So row 6's "remote panel opened OVER the trade menu" needs the chart to be openable first: the guest has to be near enough for the clone.

The recipe itself cost three failed casts to get right, and each is the same lesson the tools rules carry: `charTalkText` is a `TextMeshProUGUI` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:3521`), so `as UnityEngine.UI.Text` answered null and read as no-text on a member that exists; the first attempt also looked only at `GetField` on the declaring type. It is committed as `tools/acceptance/recipes/talk-text-read.cs` and is what the next batch reads row 6 with.