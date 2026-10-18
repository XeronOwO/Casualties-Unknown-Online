# Acceptance record — Command console ESC fully intercepted

- Ticket: `command-console-esc-not-intercepted` — verdict: **all three expected behaviours pass; the ticket
  moves to `done/`**.
- Batch: `20261018-t` — scope `docs/evidence/acceptance/20261018-t-scope.md`
- Commit: `5566b095` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5566b0954c12084df9a35fdf01faee47835d8686` (`verify-deploy.ps1` exit 0; `prelude-verify-deploy.txt`)
- Run: 2026-10-18 10:59 → 11:31 (+08:00) · Host: the physical machine; the console is a local surface
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts` —
  the Escape key was a message on the client's own window through the committed `window-key` declaration,
  after the user-authorized one-time foreground switch
- Artifacts: the `20261018-t` subdirectory of the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| C1 | A single ESC while the console is open closes the console and does not open the game's ESC menu | machine | **pass** | `a150`–`a152`: before the key the console is open (`a140`), after the posted Escape the console reads `open=0`, `paused=0`, `timeScale=1`, `ime=Auto`; the BepInEx runtime log carries `Command console ESC consumed; closing overlay.` and the CUO rolling log carries `CUO ESC-closing surface closed this frame — keeping native input modal for one frame to swallow the closing ESC.` (the CUO line at 11:22:18). `a153` re-reads `radialOpen=0 paused=0` |
| C2 | When the console is closed, ESC keeps the game's native behaviour | machine | **pass** | `a154`–`a156`: with no CUO surface up, a posted Escape toggles the game's pause (`paused=1`, the game's own `PlayerCamera.HandleInput` path); a second Escape restores it (`a157`–`a159`: `paused=0`) |
| C3 | The one-frame suppression is narrow; normal input returns on the next frame | machine | **pass** | The close's own line names the one-frame window (`CuoEscCloseSuppression`: `a152`'s CUO log line), and C2's second Escape — issued after that frame — reaches the native pause again (`a157`–`a159`), so the guard is released. The console's close also restores the IME (`a152`) |

## Residuals for the user

None: no row needs a person's judgement.

## Limits

- **The key is a window message, not an OS key press.** The client's window was in the foreground (the
  user-authorized one-time switch) and the message was queued on its own window; the OS input queue was
  never touched and no other window was addressed. The reading is the game's own `paused`/console state,
  not a pointer or a physical-keyboard event.
- **One client and one console.** The ESC-closing family's other surfaces (the Online UI window, the quick
  panel) are covered by their own batches' rows; this run exercised the console's path and the native pause
  release, not their close frames again.
- **The pause was restored** after C2's control (`a157`–`a159`); the session closed with `paused=0`.
