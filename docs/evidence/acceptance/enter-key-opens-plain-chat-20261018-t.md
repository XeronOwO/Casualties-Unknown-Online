# Acceptance record — Enter opens the console as plain chat

- Ticket: `enter-key-opens-plain-chat` — verdict: **moved back to `todo/`**: row E2 **fails** after the game's
  own console has been used; E1 and E3–E7 pass.
- Batch: `20261018-t` — scope `docs/evidence/acceptance/20261018-t-scope.md`
- Commit: `5566b095` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5566b0954c12084df9a35fdf01faee47835d8686` (`verify-deploy.ps1` exit 0; `prelude-verify-deploy.txt`)
- Run: 2026-10-18 10:59 → 11:31 (+08:00) · Host: the physical machine (the opener and the chat send); Member:
  the `Steam1` sandbox (the chat receive)
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts` —
  the keys were messages on the client's own window through the committed `window-key` declaration, after
  the user-authorized one-time foreground switch
- Artifacts: the `20261018-t` subdirectory of the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| E1 | `/` opens command mode with the `/` prefix (regression) | machine + visual | **pass** | The posted Slash opened the console (`a125`–`a127`: `open=1`, command mode); `a132` shows the `/` input and the 17 `.` suggestions; `a31` of the sibling console record reads the same opener. The first opening's field also carried a Tab-completion artifact (`/ban/`, see Limits) |
| E2 | Enter opens plain-chat mode: empty field, no `/` | machine + visual | **fail** (after the game's own console) | Before the game's console was used: `a160`–`a162` — the posted Return opens it with `open=1`, `input=` (empty), `history=1` unchanged, and the BepInEx log line `CUO console opened as plain chat: the player pressed Enter with no CUO surface up.` After the game's own console was opened and closed: `a171`–`a181` leave `gameConsoleActive=0` but the event system still names the game console's input (`selected=Console`), and `a187`–`a189` post Return with every CUO surface down — `open=0`. The guard `OnlineUiKeyboardFacts.IsATextFieldFocused` asks whether the SELECTED object carries a text field, not whether that field is focused; a closed console leaves its input selected, so the opener latches until some other selection clears it |
| E3 | The opening Enter is swallowed: no empty line is submitted, the console stays open | machine | **pass** | `a162`: after the opening Return the console is open, the input is empty and `history=1` — the `/help` sent earlier is the only entry, so no empty line was submitted |
| E4 | Enter inside the open overlay submits the line | machine | **pass** | `a137`–`a140`: `/help` typed and submitted with Return clears the input and reads `history=1`, and `a141` carries the answer lines; `a163`–`a166`: the plain line `batch-t hello` submitted with Return clears the input and reads `history=2` |
| E5 | A plain chat line reaches the other member | machine | **pass** | `a167-guest-chat.txt`: the member's own `ChatService.Recent` carries `batch-t hello` (count 1); the host's own buffer carries the same line (`a168`) |
| E6 | Enter does not open the console while another text field has focus | machine | **pass** | `a171`–`a174`: the game's own console is opened through its `console` bind (`gameConsoleActive=1`, `selected=Console`); `a176`–`a178`: the posted Return does NOT open the CUO console (`open=0`) and the game console stays active. The guard is doing its job while the field is genuinely in use |
| E7 | Enter does not open the console while a CUO surface is up | machine | **pass** | `a182`–`a185`: with the Online UI window up, the posted Return leaves the CUO console `open=0` |

## Residuals for the user

None: no row needs a person's judgement.

## The finding E2 carries

The guard is a lifetime bug, not a key-path bug. `OnlineUiKeyboardFacts.IsATextFieldFocused` reads the
game's event-system selection and answers true whenever the selected object carries a text field. The
game's own console (`ConsoleScript`) selects its input when it opens and never clears the selection when it
closes, so after a player uses the game console once, the CUO Enter/Slash opener is refused until something
else takes the selection. The row's own scenario — "Enter opens it in plain-chat mode" — is not met in that
reachable state. The fix belongs in the guard's reading (ask the field it found whether it is focused, or
check the selected object's active state, or deselect on the game console's close) and needs its own red
test; the ticket moves back to `todo/` with this record.

## Limits

- **The key is a window message, not an OS key press.** The client's window was foregrounded once with the
  user's authorization; the message was queued on its own window. No OS pointer or keyboard event was
  injected, and no other window was addressed.
- **The game's console selection.** The stale selection was produced by opening and closing the game's own
  console through its own `console` bind. The player's own path is the same toggle; the reading is the
  game's event system, not a probe-invented state.
- **The first slash's `ban`.** The posted Slash that opened the console in E1 arrived with a leftover Tab
  message and completed the first candidate (`ban`) into the field. The input was reset with the session's
  own `SetInput` before the suggestion reading; the opener itself is read as open in command mode.
