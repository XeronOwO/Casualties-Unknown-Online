# Acceptance record — the remote medical panel's speech subtitle

- Ticket: `remote-medical-panel-speech-text-desync` — verdict: **moved to `review/`** (rows 1-5 and 7-8 pass; row 6, the trade-menu sharing, stays `unproven` because the run had no trader fixture to open a trade menu)
- Batch: `20261023-b` (follow-up runs after the main batch) — sibling record `remote-medical-stage-1-injection-session-20261023-b.md`
- Commit: `a509264f` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+a509264f3a1e3409843306217c71590a11ee9691` (`deploy`/`verify` exit 0 before the clients started)
- Runs: 2026-10-19 22:41 → 22:50 · host physical + Steam1 guest
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: `.acceptance/20261023-b/` — `t-speech5-console.log`, `t-speech6-console.log`, `sv-*.json`, `sr-*.json`, `subtitle-read.cs`, `talk-force.cs`

## Root cause judged here

`PlayerCamera.HandleOnscreenCharacterTalkText` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:3065-3077`)
sets `charTalkText.text` from the LOCAL body's talker and enables it when `woundView.activeSelf ||
tradeMenu.activeSelf`. While a remote medical focus was open, that wrote the VIEWER's own line into the
subtitle. The fix (`RemoteMedicalTalkTextPatch`) takes the label over while `RemoteMedicalView.IsOpen`,
reading the viewed player's world clone (the bubble `SpeechSync` replays), clears it when the viewed
player is silent, and restores the local line on the first frame after the focus ends.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host speaks while the guest views its panel | machine | **pass** | Guest subtitle `enabled=true, text='HOSTSUB-AAA'`, clone `talk='HOSTSUB-AAA'` (`sv-guest-after-host.json`); the same string the host's own body spoke |
| 2 | Guest speaks while the host views the guest's panel | machine | **pass** | Host subtitle `enabled=true, text='GUESTSUB-CC'` (the typed prefix of `GUESTSUB-CCC`), clone `talk='GUESTSUB-CC'` (`sv-host-after-guest.json`) |
| 3 | The viewed player is silent | machine | **pass** | After the host's line clears: guest subtitle `text=''` while the remote focus is still open (`sv-guest-cleared.json`) |
| 4 | The viewer speaks while viewing a peer | machine | **pass** | Guest speaks `GUESTSUB-BBB` while viewing the host: the guest subtitle stays `text=''` (`sv-guest-after-guest.json`) — the viewer's own line never replaces the viewed player's |
| 5 | Close the remote panel, then open the LOCAL panel | machine | **pass** | Exact blocker order (`t-speech6`): the peer line was in the label (`text='HOSTB1-AA'`, `sr-guest-remote-line.json`); 300 ms after `medical-view-close` the label was `enabled=false, text=''` (`sr-guest-after-close.json`), still empty at 1 s, and opening the LOCAL panel while silent read `enabled=true, text=''` (`sr-guest-local-panel-silent.json`) — the peer's line did not reappear |
| 6 | Trade menu open, remote panel opened over it | machine | **unproven** | no trader fixture was staged in this run; the label and the restore path are shared (`charTalkText`, `woundView.activeSelf \|\| tradeMenu.activeSelf`), but the trade-menu order was not observed |
| 7 | Target leaves while the panel is open | machine | **pass** | The guest quit; the coordinator closed the focus, the host subtitle read `enabled=false, remoteOpen=false, text=''` (`sv-host-target-left.json`) |
| 8 | Panel closed entirely | machine | **pass** | `enabled=false` after `medical-view-close` (`sv-guest-closed.json`, `sr-guest-after-close.json`); the native enabled rule is untouched |

## Residuals for the user

None: the reported mismatch (the viewer's own line in the viewed player's panel) is gone in both
directions; what remains is the trade-menu row, which the agent could not stage.

## Limits

- Row 6 was not staged: no trader was available in the two-client world; the shared-label restore is
  the same code path as row 5.
- The lines were driven through each body's own `Talker.Talk` (force) rather than the B-key bark; the
  subtitle path and the speech report are the same, but the bark key itself was not pressed.
- The subtitle is judged from its own text/enabled state (`charTalkText`), not from a captured frame;
  the text is the row's subject, and the machine read is the same value the frame would show.
