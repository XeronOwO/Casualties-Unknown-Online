# The remote medical panel's identity header shows the viewer's own data

- Status: Done — accepted in batch `20261024-a` (rows 1-6 pass); record `docs/evidence/acceptance/remote-medical-panel-header-shows-viewer-identity-20261024-a.md`
- Priority: High
- Category: Remote medical / WoundView display / identity header
- Source: User report (2026-10-19, during acceptance batch `20261023-b`): "现在查看他人面板，但是面板顶部显示的文本是不同步的". The user later clarified the sentence they meant was the character SPEECH subtitle, which has its own ticket (`review/remote-medical-panel-speech-text-desync.md`), and that the identity header "可能也是问题"; the values below are this ticket's own live evidence that the identity line IS the viewer's, whichever report named it.
- User ruling (2026-10-19): the header must show the VIEWED player's own data.

## Problem

`WoundView.SetCharDetails(height, age, id, ver)` fills the panel's identity header
(`bodyStatText` = `"<height>cm | <age>y | #<id>"`, `versionText` = `"UNI-HEALTH v2.0" + ver`,
`cInfo` = the four ints). The game sets it exactly once, from the LOCAL character:

- a new run: `PlayerCamera.Start` calls it with four `Random.Range` values
  (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:728`);
- a loaded run: `SaveSystem` restores the run save's `cHeight`/`cAge`/`cId`/`cVer`
  (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:440`).

Nothing rewrites it afterwards — `WoundView.UpdateView` never touches the two labels — so a
remote medical focus that does not take the header over keeps the VIEWER's identity over the
viewed player's body.

Live evidence (batch `20261023-b`, the guest viewing the host, the same session):

| client | cInfo (height, age, id, version) | bodyStatText | versionText |
|---|---|---|---|
| guest (viewing the host) | 135, 23, 44960, 6 | `135cm \| 23y \| #44960` | `UNI-HEALTH v2.06` |
| host (its own panel) | 148, 18, 83756, 8 | `148cm \| 18y \| #83756` | `UNI-HEALTH v2.08` |
| alt (its own panel) | 154, 12, 80646, 6 | `154cm \| 12y \| #80646` | `UNI-HEALTH v2.06` |

The guest's panel therefore showed the guest's own random identity while its body, heart,
breathing, mood and limb state were correctly the host's.

The prefab's `nameText` ("EXPERIMENT") is a separate, static label: nothing in
Assembly-CSharp writes `WoundView.nameText`, so it reads the same for every player, the
local panel included. It is out of scope here unless the user says that line must change
too.

## Design (decided by the user)

1. The viewed player's four identity values come from the character snapshot the remote medical
   view already reads, drawn from the owning client's OWN `WoundView.view.cInfo` (which is the
   local character's values, random on a new run and the save's values on a loaded run). The
   carrier is named under *Carrier* below — the original sentence named `CharacterHealthMsg`,
   which was this ticket's guess before the snapshot was read.
2. While a remote medical focus is open, the displayed player's four values are written through
   `WoundView.SetCharDetails` (the game's own entry) whenever they change — not on every snapshot
   tick: the native call rewrites both labels and allocates the four ints, and the values only
   change on a re-roll or a forced write.
3. On close, restore the VIEWER's own four values and the two texts — the native panel is
   shared with the local view, so a closed remote focus must leave the local header exactly
   as it was found.
4. No parallel panel and no CUO-drawn text: the native `WoundView` remains the only
   surface.

## Carrier: the four values already travel (premise corrected 2026-10-20)

Design item 1 is already live — the ticket was written without knowing it, and the check that
found it also found the two hazards the carrier creates:

- `CharacterDataMsg.NativeFields` (`CharacterNativeFieldsMsg.CharacterInfo`, decision 171) rides
  every character snapshot whose capture succeeded — not only the save: `CharacterDataCapture.Capture`
  fills it for the 1 Hz report, the host's broadcast and the mid-run cut, and
  `CharacterNativeFields.TryCapture` reads it from the sender's own `WoundView.view.cInfo`
  (`CharacterNativeFields.LiveSystem.Read`). The host keeps the sub-message in its saved character
  and relays it unchanged to the other guests (`CharacterDataStore.RelayCharacterData` sends the
  same `CharacterDataMsg`), so every peer already receives every other player's four values in
  every direction. A failed read is null rather than zeros (`CharacterNativeFieldPolicy.TryCapture`),
  and `CloneFactTable.ApplyMedicalState` can hold an entry that carries none — the unknown-identity
  case below is that state, not a hypothesis.
- Therefore no second carrier is added: a copy on `CharacterHealthMsg` would be the same fact in
  two places, and the existing sub-message is the one the restore path already proves.
- Hazard 1 — the client's own identity: the panel's `cInfo` is the ONLY carrier of the LOCAL
  character's identity in the live game. Once the focus writes the viewed player's values there,
  this client's own 1 Hz snapshot would report the VIEWED player's identity as its own — and every
  peer that views this client would then show that identity. So the borrow answers the local
  identity read with the remembered copy, and a local character restore that lands mid-focus
  updates that copy instead of writing the borrowed panel.
- Hazard 2 — the game's own save: `SaveSystem.SaveGame` copies the four values straight out of
  `WoundView.view.cInfo` into `save.sv` (`SaveSystem.cs:172-175`), bypassing the guard. While a
  borrowed panel is open that save would persist the viewed player's identity as the local
  character's, so a capture window shows the viewer's own values for the duration of the native
  call and re-asserts the focus's display afterwards.

## What landed (2026-10-20)

| Mechanism | Change | Evidence |
|---|---|---|
| Panel header takeover | `RemoteMedicalHeader.ShowViewedIdentity` (called by `RemoteMedicalCoordinator.Open` and by its per-frame `Update`) writes the viewed player's four values through the native `WoundView.SetCharDetails`; the native write happens only when the values change, so the per-frame path is a comparison | `RemoteMedicalHeaderTests.Refresh_ShowsTheViewedPlayersValues_AndKeepsTheViewersOwnForTheSnapshot`, `Refresh_SameValuesAgain_DoesNotRewriteThePanel`; `TheFocusFollowsTheSnapshotsIdentity_OnOpenAndOnEveryRefresh` pins the open-time takeover (after the native panel opened) and the per-frame call site |
| Close restore | `RemoteMedicalView.Close` gives the viewer's own values back through the same native entry, before its early return — the one close path every teardown goes through | `RemoteMedicalHeaderTests.End_GivesTheViewersOwnValuesBackOnce`, `TheClosePath_GivesTheHeaderBackBeforeItCanSkipTheRestore` |
| Local identity read | `CharacterNativeFields.LiveSystem.Read` takes the identity from the borrow while the panel is borrowed, so this client's own snapshot keeps reporting its own identity; `SetCharacterDetails` (the restore path's write) adopts into the borrow instead of overwriting the panel | `OwnIdentity_AStaleBorrow_DoesNotAnswerForThePanel`, `TryAdoptOwn_WhileBorrowed_KeepsThePanel`, `TheLocalIdentityPort_UsesTheBorrowedPanelsRememberedValues` |
| Unknown identity | A snapshot that carries no identity (a sender whose scene could not be read) blanks the two labels rather than showing the viewer's own; `cInfo` and the two statics keep describing the viewer. A later snapshot writes the labels back through the native entry. The blank writes the labels only, so it must not lose which four values the panel carries — that was review finding B1 | `Refresh_NoIdentity_BlanksOnceThenShowsItWhenItArrives`, `Refresh_AnIdentityThatIsNotTheNativeFour_IsTreatedAsUnknown`, `End_AfterABlank_StillGivesTheLabelsBack`, `Refresh_ShowThenBlank_KeepsTheCaptureGuardAndTheCloseRestore`, `Refresh_ShowThenBlankThenShowAgain_RestoresTheLabelsAndTheBorrow`, `TryAdoptOwn_AfterABlank_StillKeepsTheBorrowedPanel` |
| Stale borrow | A borrow is only honoured while the panel still carries what the focus wrote; a reloaded scene's own `SetCharDetails` drops it instead of being overwritten by the old character's values | `End_AfterThePanelWasRewritten_LeavesTheNewerValuesInPlace`, `End_AfterABlankAndARewrittenPanel_LeavesTheNewerValuesInPlace`, `Refresh_ANewIdentityOnTheSnapshot_FollowsIt` |
| Native save reader | `RemoteMedicalNativeSaveIdentityPatch` wraps `SaveSystem.SaveGame` in `RemoteMedicalHeader.EnterOwnIdentityCapture()`: the save sees the viewer's own four values, and the close's display is re-asserted afterwards (after a blank: values back, labels blanked again) | `OwnIdentityForCapture_IsTheViewersOwnOnlyWhileThePanelIsBorrowed`, `Reassert_PutsTheFocusDisplayBackAfterATemporaryCapture`, `TheNativeSave_SeesTheViewersOwnIdentityAndNotTheBorrowedHeader` |
| Wire | Unchanged: the values already ride `CharacterDataMsg.NativeFields` (decision 171), so the protocol number stays the frozen pre-release baseline | `CharacterDataMsg.cs` (`[ProtoMember(9)] NativeFields`), `CharacterDataCapture.Capture` |

The panel's other native texts keep their existing sources: the limb readouts come from the
display body (the `WoundView.body` patch), the ECG from `ECGVisualizer.get_body`, the speech line
from `RemoteMedicalTalkTextPatch`. The header was the one label family with no display source.

The independent review of this cycle found the two defects above (a blank losing the panel's
`cInfo`, and the unguarded native save) before the commit; both are fixed in the same commit, and
the blank's fix has a red→green record: the four ordering cases fail on the pre-fix rule
(`.acceptance/20261024-a/red-header-blank-order.log`, 3 failures) and pass after it
(`focused-header-tests.log`, 23/23).

## Acceptance rows

| # | Scenario | Expected |
|---|---|---|
| 1 | Open a remote player's panel on a peer whose header differs | `cInfo`/`bodyStatText`/`versionText` equal the viewed player's own values |
| 2 | Close the remote focus | The viewer's own header returns, text for text |
| 3 | All three clients and both directions | Each viewer sees the other's header, never its own |
| 4 | A loaded run | The saved `cHeight`/`cAge`/`cId`/`cVer` travel, not a fresh random set |
| 5 | A snapshot refresh while the focus is open | The header follows the latest committed values, with no flicker back to the local values |
| 6 | A third client views the VIEWER while the viewer has a remote panel open on someone else | The third client sees the VIEWER's own identity — the viewer's snapshot never reports the identity its panel is displaying |

Row 6 was added by this cycle's independent review: it is the observable form of hazard 1 above,
and without it the guard could be dropped and every two-client row would still pass.

## Limits

- The two statics `SetCharDetails` also writes are restored through the same native entry on close.
  `firmwareVer` has no reader in Assembly-CSharp at all, and `specimenId` is read only by the
  death-stats screen's `#<specimenId>-SAW-01` (`PlayerCamera.cs:2315`). A teardown that destroys
  the panel without a close (a scene reload) leaves them at the viewed player's values until the
  game's own next `SetCharDetails`; nothing reads them before that.
- The unknown-identity blank and the stale-borrow drop are unit-tested only: staging a live
  capture failure or a scene reload mid-focus is not part of the batch. The mid-focus restore's
  adopt seam is exercised live (a kill/Continue of the viewed player while the peer's panel is
  open) and also unit-tested.
- The native-save guard is unit- and source-pinned only: triggering `SaveSystem.SaveGame` with a
  remote panel open needs the console's `saveandquit` (which leaves the world) or the end-of-layer
  panel (not reachable under the full-screen WoundView), so the batch cannot observe it without
  ending the session.
- The identity is per character, so mid-focus refreshes only ever change it after a forced write
  or a re-roll; row 5 is judged by forcing the owner's `cInfo` and waiting one snapshot, and its
  "no flicker" half is judged from sampled reads plus the write-on-change rule, not a per-frame
  recording.

## Non-goals

- No parallel CUO medical panel; no CUO-drawn text.
- The prefab's static "EXPERIMENT" `nameText` label is not part of this ticket (see above).
