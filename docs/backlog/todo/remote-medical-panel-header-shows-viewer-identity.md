# The remote medical panel's identity header shows the viewer's own data

- Status: Todo
- Priority: High
- Category: Remote medical / WoundView display / identity header
- Source: User report (2026-10-19, during acceptance batch `20261023-b`): "现在查看他人面板，但是面板顶部显示的文本是不同步的" — the top line of the panel (the `height | age | id` text and the `v` version beside it) keeps the viewer's own values while the panel is showing another player.
- User ruling (2026-10-19): the header must show the VIEWED player's own data.

## Problem

`WoundView.SetCharDetails(height, age, id, ver)` fills the panel's identity header
(`bodyStatText` = `"<height>cm | <age>y | #<id>"`, `versionText` = `"UNI-HEALTH v2.0" + ver`,
`cInfo` = the four ints). The game sets it exactly once, from the LOCAL character:

- a new run: `PlayerCamera.Start` calls it with four `Random.Range` values
  (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:728`);
- a loaded run: `SaveSystem` restores the run save's `cHeight`/`cAge`/`cId`/`cVer`
  (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:440`).

None of the four values is part of any character snapshot, and nothing in
`RemoteMedicalCoordinator`/`RemoteCharacterDisplayProjection` projects them, so a remote
medical focus keeps the VIEWER's header.

Live evidence (batch `20261023-b`, the guest viewing the host, the same session):

| client | cInfo (height, age, id, version) | bodyStatText | versionText |
|---|---|---|---|
| guest (viewing the host) | 135, 23, 44960, 6 | `135cm \| 23y \| #44960` | `UNI-HEALTH v2.06` |
| host (its own panel) | 148, 18, 83756, 8 | `148cm \| 18y \| #83756` | `UNI-HEALTH v2.08` |
| alt (its own panel) | 154, 12, 80646, 6 | `154cm \| 12y \| #80646` | `UNI-HEALTH v2.06` |

The guest's panel therefore shows the guest's own random identity while its body, heart,
breathing, mood and limb state are correctly the host's.

The prefab's `nameText` ("EXPERIMENT") is a separate, static label: nothing in
Assembly-CSharp writes `WoundView.nameText`, so it reads the same for every player, the
local panel included. It is out of scope here unless the user says that line must change
too.

## Design (decided by the user)

1. Carry the four ints on the character snapshot the remote medical view already reads
   (the 1 Hz `CharacterHealthMsg` path / its captured presentation), drawn from the owning
   client's OWN `WoundView.view.cInfo` (which is the local character's values, random on a
   new run and the save's values on a loaded run).
2. While a remote medical focus is open, write the displayed player's four values through
   `WoundView.SetCharDetails` (the game's own entry) every time a snapshot refreshes.
3. On close, restore the VIEWER's own four values and the two texts — the native panel is
   shared with the local view, so a closed remote focus must leave the local header exactly
   as it was found.
4. No parallel panel and no CUO-drawn text: the native `WoundView` remains the only
   surface.

The wire change is the kind decision 241 governs: this project is pre-release, so the
protocol number stays the frozen baseline and the change is recorded by this ticket, its
commit and the sync-coverage matrix.

## Acceptance rows

| # | Scenario | Expected |
|---|---|---|
| 1 | Open a remote player's panel on a peer whose header differs | `cInfo`/`bodyStatText`/`versionText` equal the viewed player's own values |
| 2 | Close the remote focus | The viewer's own header returns, text for text |
| 3 | All three clients and both directions | Each viewer sees the other's header, never its own |
| 4 | A loaded run | The saved `cHeight`/`cAge`/`cId`/`cVer` travel, not a fresh random set |
| 5 | A snapshot refresh while the focus is open | The header follows the latest committed values, with no flicker back to the local values |

## Non-goals

- No parallel CUO medical panel; no CUO-drawn text.
- The prefab's static "EXPERIMENT" `nameText` label is not part of this ticket (see above).
