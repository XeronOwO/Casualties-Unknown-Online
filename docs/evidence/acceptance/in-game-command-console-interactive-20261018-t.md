# Acceptance record — Interactive in-game command console

- Ticket: `in-game-command-console-interactive` — verdict: **all rows pass; the ticket moves to `done/`**.
  The ticket states its expectations in prose (the 2026-09-05 rejection list and the redo's claims); the row
  table below was written before the run, in the batch plan.
- Batch: `20261018-t` — scope `docs/evidence/acceptance/20261018-t-scope.md`
- Commit: `5566b095` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5566b0954c12084df9a35fdf01faee47835d8686` (`verify-deploy.ps1` exit 0; `prelude-verify-deploy.txt`)
- Run: 2026-10-18 10:59 → 11:31 (+08:00) · Host: the physical machine (the console is a local surface); a
  member was in the world, used only by the sibling Enter record's chat row
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts` —
  the key rows use the committed `window-key` declaration after the user-authorized one-time foreground
  switch
- Artifacts: the `20261018-t` subdirectory of the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| B1 | The panel is compact, translucent and anchored at the bottom, with the world visible behind the history | visual | **pass** | `a129-console-open.png` and `a132-console-suggest.png`: a bottom-centred overlay over the world, the history and the suggestions painted on a translucent dark strip |
| B2 | The `>` prompt and the input box are aligned in one row | visual | **pass** | `a132-console-suggest.png`: `>` and the `/` input sit on the same baseline in one row, with the caret at the input's start |
| B3 | No header / key-instruction line is drawn | visual | **pass** | `a129`/`a132`: the history starts at the first line; there is no title or `回车/Tab/ESC` instruction row above it |
| B4 | `/` opens it and immediately shows live suggestions; typing narrows them | machine + visual | **pass** | The posted Slash opened the console (`a126`: `open=1`, the input began with the command prefix); `a131`: the session's `LiveSuggestions` at `/` is 17 entries (`ban`, `clear`, `data`, `deop`, `give`, `heal`, `help`, `hostrules`, …); `a133`: `/he` narrows it to 2 (`heal`, `help`); `a132` shows the list above the input |
| B5 | While open, the full history is shown with no fade | machine + visual | **pass** | `a129-console-open.png` and `a132-console-suggest.png` paint every history line at full alpha; the open overlay's `DrawHistory` path has no fade, and the frames show the lines down to the input row |
| B6 | Fading applies only to the closed-panel notification strip | visual + machine | **pass** | `a197-notification-strip.png` (a few seconds after the ESC close) shows the newest lines as a dim fading strip at the bottom; `a198-notification-gone.png` (16 s later) has no strip. The open panel (`a132`) is not faded |
| B7 | ESC closes the console and does not open the pause menu | machine | **pass** | `a150`–`a152`: the posted Escape leaves `open=0`, `paused=0`, `ime=Auto`; the BepInEx runtime log carries `Command console ESC consumed; closing overlay.` and the CUO log carries the one-frame modal line (the sibling ESC record lists both). The same reading is the sibling ticket's C1 |
| B8 | While open, the input state blocks other game interaction | machine | **pass** | `a146`–`a149`: with the console open, a posted `toggleinventory` (Tab) leaves `PlayerCamera.radialOpen=0`; the inventory ring does not open. With the console closed the same key drives the game again (the channel's own smoke `a121`–`a124`) |
| B9 | Tab completion and Up/Down history work inside the console | machine | **pass** | `a136`: with `/he`, Tab completes the input to `/heal`; `a145`: with the field cleared, Up recalls `/help` from the session's history |
| B10 | `/help` still lists the commands | machine | **pass** | `a141-console-lines.txt`: the `/help` answer line lists every command with its one-line purpose (`/clear`, `/players`, `/rtt`, `/whoami`, `/heal`, `/hostrules`, `/kick`, `/ban`, `/unban`, `/op`, `/deop`, `/spectate`, `/save`, `/data`, `/give`, `/summon`) |
| B11 | The input enables IME while open and restores it on close | machine | **pass** | `a126`/`a140`: `ime=On` while the console is open; `a152`/`a181`: `ime=Auto` after it closes (and after the game's own console was opened and closed) |

## Residuals for the user

None: no row needs a person's judgement.

## Limits

- **The first opening carried a completion artifact.** The posted Slash that opened the console arrived with
  a Tab message left from the inventory-channel smoke, and the console's completion put the first candidate
  (`ban`) into the field: `a126` reads `input=/ban/`. The input was reset with the session's own `SetInput`
  before the suggestion reading (`a131`); the opener itself is read as `open=1` in command mode, and the
  artifact is the run's own key-channel history, not the console's opening text.
- **The key channel needs the client in the foreground.** The posted keys were ignored while the host
  window was not the foreground (`a111`/`a112`: `posted=true`, `heldAtEnd=false`); after the user-authorized
  one-time foreground switch the same channel worked (`a121`–`a124`). The one switch is recorded in the
  scope; no OS pointer or keyboard event was injected.
- **One client.** The console is a local surface; no guest view of it is claimed. The chat half of the
  sibling Enter ticket used the member's own buffer.
