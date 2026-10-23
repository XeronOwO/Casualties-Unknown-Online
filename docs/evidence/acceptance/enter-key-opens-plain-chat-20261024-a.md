# Acceptance record — Enter opens plain chat after the game's own console is used

- Ticket: `enter-key-opens-plain-chat` — row E2 now **passes**; moved to `done/`. Rows E1 and E3-E7 were
  judged in batch `20261018-t` (`docs/evidence/acceptance/enter-key-opens-plain-chat-20261018-t.md`); this
  batch re-judges E2 and re-reads the console-open guard E6 rests on.
- Batch: `20261024-a` — two Steam clients in one world.
- Commit: `575ea2e3` · Deployed artifact: `0.1.0+575ea2e355bf3dcea011e50f3a89b6ef25021d95` —
  `tools/verify-deploy.ps1` exit 0 (`enter-verify.log`), and the probe printed the identity again in its own
  log (`enter-green.log`, first line).
- Run: 2026-10-24 02:56 → 02:57, this session's clock · Host: physical machine · Member: first sandbox.
- Artifacts: `enter-green.log`, `enter-green-*.json`, `enter-green-verify.txt`,
  `enter-green-session-environment.txt`, the frame `enter-green-after-enter.png`, and the red pair
  `enter-red2.log` / `enter-red2-after-enter.png`, in the directory named by `acceptance-artifacts-dir`.
- Method: the host's Online UI was closed, the `window-key` declaration loaded, the game's own console
  toggled through its `console` bind (`key-hold bind=console`, posted as window messages on the client's own
  window), then Return posted through `key-tap`; `game-console-focus` read the game console's `active` flag,
  the event system's selection and activity, the selected object's input components and the adapter's guard
  (through reflection). No OS input queue, focus change or other window was touched; the foreground call
  reported `set=False` and the messages still landed, so the run proves the in-process path.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| E2 | Enter opens plain-chat mode: empty field, no `/`, after the game's own console has been used | **pass** | `enter-green.log`: baseline `guard=false`; game console open `active=true selected=Console selectedActive=true guard=true`; closed `active=false selected=Console selectedActive=false` **`guard=false`** (pre-fix the same state read **`guard=true`**, `enter-red2.log`); after Return `guard=false`; `enter-green-after-enter.png` shows the CUO console open with the banner `CUO command console ready. Type /help for available commands, or just type to chat.` and an EMPTY input line (plain chat, no `/`) |
| E6 (re-read) | Enter does not open the console while the game's own console is active | **pass** | the same run's `active=true` step reads `guard=true`, the fact the opener asks; the game console's open state is now read from `ConsoleScript.instance.active`, so it holds even if the selection moves |
| E1, E3-E7 | `/` command mode, the swallow, submit inside the overlay, chat delivery, CUO-surface gating | carried from `20261018-t` | unchanged by this cycle; the shared opener gate is the only code touched, and E1's `/` path uses the same guard |

## Before / after

- Pre-fix artifact `0.1.0+2dfe2071…`: after the game console was opened and closed, the guard stayed
  `true` and Return opened nothing (`enter-red2.log`, `enter-red2-after-enter.png`).
- Fixed artifact `0.1.0+575ea2e3…`: the same steps read `guard=false` and Return opens plain chat
  (`enter-green.log`, `enter-green-after-enter.png`).

## Limits

- **One host/member pair, one repetition.** A race between the toggle and a read is not excluded by one run;
  the canvas slide is ~0.29 s and the probe waited 1.6 s after the close before reading.
- **The frame is the host's.** The member's client shares the same local guard; no per-role difference exists
  in this reading.
- **The slide-out window is dead, not double-used**: while the console's canvas is still active
  (~0.29 s after `active` flips false) the guard stays true and Enter is ignored; the game ignores Enter in
  the same window. Recorded in the ticket's Limits.
- **The settings key-bind capture is not covered** and is filed as `todo/enter-opener-keybind-capture.md`;
  this run did not open a key-bind row.
- **Steam clients were left running**; both game clients were closed by the driver and the process list was
  empty afterwards.
