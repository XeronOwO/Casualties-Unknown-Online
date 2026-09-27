# The CUO Online launcher button covers the play area

- Status: Review
- Priority: Medium
- Category: Online UI / presentation
- Source: User acceptance finding (2026-09-21): the `CUO 联机` button in the top-right corner blocks the game view; the user asks for a design fix, for example becoming semi-transparent after a period without use.
- Related: `review/remove-the-online-ui-console-page.md` (the other Online UI window change from the same acceptance pass), `done/player-list-polish.md`, `review/online-ui-layout-and-input-detail-pass.md` (the 2026-09-27 acceptance pass found the 0.35 floor still covered the medical panel's readout in the same corner, and dropped it to 0.12 there)

## Evidence

- `OnlineUiWindow.DrawLauncherButton` draws a fixed `new Rect(Screen.width - 170f, 12f, 158f, 34f)`
  with an opaque background every frame (`OnlineUiTheme.DrawBackground` plus
  `OnlineUiTheme.Launcher`). There is no idle state, no fade and no way for the player to shrink or
  move it, so it always covers the same part of the world view.

## Requirement and design

- The launcher must not obstruct the view while it is not being used: after a short idle window (on
  the order of a few seconds) it fades to a clearly translucent state; hovering it restores full
  opacity; it stays clickable in both states and does not flicker while the window itself is open.
- The idle timer is presentation-only local state (the `OnlineUiWindowState` family), never session
  state, and the fade must not allocate per frame.
- The exact alpha, idle delay and whether the fade is instant or eased are the implementation
  cycle's call; the visual result is confirmed in the user's acceptance run.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | No interaction for the idle window | The launcher is translucent and the world behind it is readable |
| 2 | The cursor moves over it | Full opacity, immediately |
| 3 | Clicked in either state | The window opens and closes exactly as today |
| 4 | The window is open and the mouse is elsewhere | No flicker, no per-frame allocation increase |
| 5 | Solo menu / no session | Same behaviour |

## What landed (2026-09-26)

- **A pure fade rule.** `OnlineUiLauncherFade` (Runtime/OnlineUi) owns the last-activity stamp and
  returns the launcher's opacity from `(nowMs, hovered)` alone: opaque for a 4 s idle window, a
  600 ms linear ramp down to a 0.35 floor, full opacity immediately on hover, and the idle window
  restarting from that hover. Equal timestamps return equal alpha, so IMGUI's Layout and Repaint
  passes cannot double-count the idle time, and a backwards clock (the `Environment.TickCount` wrap)
  counts as activity instead of an enormous idle age. One instance is held by `OnlineUiWindowState`
  as presentation-only state.
- **The alpha reaches the control.** `OnlineUiWindow.DrawLauncherButton` takes the pointer fact from
  `Event.current.mousePosition` and folds the alpha into both visible halves —
  `OnlineUiTheme.DrawBackground(rect, alpha)` for the frame and `OnlineUiTheme.Launcher(alpha)` for
  the label. The first cut of this cycle pushed the alpha through `GUI.color` instead; the
  independent review proved with the game's own `UnityEngine.IMGUIModule.dll` IL that the
  explicit-colour `DrawTexture` overload takes its colour verbatim, so the tint dimmed only the label
  and left the panel at alpha 0.96 — the reported defect intact. The launcher's frame now also draws
  with `alphaBlend: true` (the shared panels followed in
  `review/online-ui-panels-request-alpha-blend-false.md`, so every themed frame blends now).
- **No per-pass allocation.** The label comes from a cached pair in `OnlineUiWindowState` instead of
  a per-pass concatenation, so the draw path builds no string, and the launcher style is re-derived
  only when the alpha changes — a `GUIStyleState` access allocates, and the launcher holds one alpha
  for seconds at a time. The 600 ms ramp costs three `GUIStyleState` wrappers for every pass that
  changes the alpha; that residual is recorded in the self-check rather than fixed, because the
  alternative holds native style-state pointers this cycle cannot verify.
- **The rect and the click are unchanged** (a fade, not a move), and the cycle touches no wire,
  protocol, save, session or gameplay code.
- **Sibling resolved in its own cycle:** the theme's shared panels asked for `alphaBlend: false`, which
  could make their claimed translucency unattainable; that question was carried by
  `review/online-ui-panels-request-alpha-blend-false.md`, whose cycle made every themed frame blended
  (the launcher's own frame already was).

Red, ladder, the adversarial review's dispositions and the limits (no rendered pixels here — the
acceptance run is the judge): `docs/evidence/selfchecks/ui/cuo-launcher-idle-fade-selfcheck.md`.

## Non-goals

- Not replacing the Online UI window framework and not moving the window itself.
