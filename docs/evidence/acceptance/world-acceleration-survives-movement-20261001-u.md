# Acceptance record — Manual world acceleration must not end when a player moves

- Ticket: `world-acceleration-survives-movement` — verdict: **moved to `done/`**
- Batch: `20261001-u` — tickets `world-acceleration-survives-movement`, `world-time-local-initiation`,
  `world-acceleration-quake-direct-write`
- Commit: `8a0ddafb` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+8a0ddafb…` (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output")
- Run: 2026-10-01 18:31 → 18:46 · Host: physical machine (launched through Steam, `steamInit` true) ·
  Guest: the primary sandbox (reads through the physical deploy — no shadow to clear)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `logs`, `artifacts`, `capture`,
  `input` (preflight: 11 present, exit 0); `sandbox-alt` present and not needed
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir` (`batch-u`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host starts the acceleration, then walks | machine | **pass** | `u-c2-host-announce-fast` answers 5×; `u-c4-host-drive` + `u-c5-host-body-after` move the body (0,504.6 → 18.6,499.4); `u-c6` runs the movement rule's own silent call and `u-c7` still reads 5×; the guest reads 5× in `u-c3` and `u-c8`; host log `silent Normal reset ignored — the session clock stands at Fast.` |
| 2 | Guest starts the acceleration, then walks | machine | **pass** | `u-d4-guest-announce-fast` answers 5× in its own call; `u-d6`/`u-d7` move the body (0,504.6 → 24.1,499.6); `u-d8`/`u-d9` read 5× after the movement call; `u-d10` reads the host at 5×; guest log carries the same swallowed line |
| 3 | A teammate walks while the session is accelerated | machine | **pass** | the guest's own log holds a burst of `silent Normal reset ignored — the session clock stands at Fast./SuperFast.` at 18:40:12 while the host's acceleration stood, and `u-o3`/`u-o4` show the teammate's clock on the session value with the host's clock unchanged |
| 4 | Anyone mines, eats or uses an item while accelerated | machine | **pass**, one confound named | `u-e1-host-block-hit` (block 2 damaged) + `u-e2` (5×); `u-e3-host-world-drink` + `u-e4` (5×); `u-e5` (the guest 5×); `u-f2` ran the item use, but the read after it (`u-f3`) caught the earthquake that started at 18:34:34 — that instance is confounded and is named in Limits |
| 5 | The accelerate key is pressed back to 1× | machine | **pass** | host key: `u-d1` (1×) + `u-d2` (the guest 1×); guest key: `u-g1` (5×), `u-g3` (1×) + `u-g4` (the host 1×); host log `host accepted Normal request …` twice |
| 6 | Everyone falls asleep | machine | **pass** | `u-l1`/`u-l2` put both bodies down; `u-l3`/`u-l4` read 25× `UnconsciousFast` on both; host log `host policy applied UnconsciousFast (next request Normal)`; `u-l10`/`u-l11` show waking returns both to 1× |
| 7 | Pause, menu, death and the start gate | machine | **pass** | pause/menu: `u-m2`–`u-m7` (the host alone drops to 0× `Paused`, the guest stays 5×, unpausing returns both to 1×); death: `u-p3`–`u-p5` (the guest's body dies and the shared clock stays 5× — the transition writes nothing); start gate: host log `silent Normal reset swallowed — the start gate owns the world clock.` at 18:32:55 |
| 8 | The HUD speed indicator and its sound | machine + residual | **pass** (indicator) | the game's own HUD widgets read live on both clients: `x5` with icon 1 lit at Fast and `x1` with icon 0 lit at Normal (`u-i2`, `u-i3`, `u-i5`, `u-i6`, `u-j1`); nothing is written when the clock already is the session speed, so the switch sound cannot fire on a movement event (the 30-swallow burst above). The audible half is a residual — no frame carries a sound and this run recorded none |
| 9 | Enforcement never fights the standing acceleration | machine | **pass** | the clock sampled repeatedly across the movement events (`u-c10`, `u-d11`, `u-h1`, `u-h2`) stays exactly on the applied value — no dip and no correction ramp anywhere in the window log |

## Residuals for the user

- Row 8's sound half: the run proves no clock write (and therefore no switch-sound call) happens on a
  movement event, but it cannot capture audio; a person listening through an acceleration and a
  movement key is the only way to confirm the absence audibly.

## Limits

- **The OS key press itself is not synthesizable.** A row runs the same native call the key's handler
  makes (`PlayerCamera.SetTimeScale` with the same flags, `PauseHandler.TogglePause`,
  `WorldGeneration.earthquakeDelay`), never Unity's `Input`; that layer is vanilla and untouched.
- Row 4's item-use instance is confounded by the earthquake that started in the same window (host log
  18:34:34.010) — mining and drinking carry the row on their own.
- The speed widget is active and its state is read from the live UI objects, but it is not composited
  into the window-level capture the run took (`u-hud2-*`); the frames are kept as context, not as the
  row's evidence.
- A single session cannot exclude a rare race; no row here claims to.
