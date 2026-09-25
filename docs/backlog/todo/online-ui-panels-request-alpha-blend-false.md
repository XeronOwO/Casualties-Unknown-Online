# The Online UI's panels ask for alphaBlend: false

- Status: Todo
- Priority: Low-Medium
- Category: Online UI / presentation
- Source: The launcher idle-fade cycle's independent review (2026-09-26): repairing that fade established that the theme's shared panel draws pass `alphaBlend: false`, and that the explicit-colour `GUI.DrawTexture` overload hands its colour — and that flag — to the native draw verbatim.
- Related: `review/cuo-launcher-button-obscures-the-view.md` (whose frame now takes the blended path), `docs/evidence/selfchecks/ui/cuo-launcher-idle-fade-selfcheck.md`, `docs/evidence/selfchecks/ui/online-ui-window-selfcheck.md`

## The question

`OnlineUiTheme.DrawBackground(rect)` and `OnlineUiTheme.DrawOverlayBackground(rect)` draw with
`GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, <colour>, 0f, 0f)`.
The fourth argument is `alphaBlend`, and every colour they hand over carries an alpha below 1 —
`Panel` 0.96, `Border` 0.9, `OverlayPanel` 0.58. Unity documents that argument as whether alpha
blending is applied when the image is drawn (enabled by default), and the module's managed IL selects
the blit material rather than the blend material when it is false.

The theme's own comments read the other way: "a dark, translucent 'operator console' look", and for
the command console "this remains translucent so the world stays visible behind the command history".
Both cannot be true.

## What decides it, and the repair on each side

The rendered frame decides — whether the modal window, the quick panel, the context menu and the
console history are translucent or opaque today. That is the user's acceptance run's answer; the
native `Graphics::Internal_DrawTexture` body is not inspectable here, so this repository cannot settle
it by reading code.

- If they are opaque, the flag is the cause: draw with `alphaBlend: true` (the launcher's own frame
  already does — see the idle-fade cycle) or correct the colours, then re-check every surface that
  uses the theme: the modal window, the quick panel, the context menu, the console overlay and the
  launcher.
- If they are translucent, the flag does not suppress blending for these draws, and the two comments
  above are the only wrong thing: correct them and close this ticket.

## Non-goals

- Not part of the launcher's idle fade: that frame takes the blended path and needs no answer here.
- Not a theme redesign; the palette, the fonts and the layout stay as they are.
