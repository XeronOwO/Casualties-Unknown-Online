# The Online UI's panels asked for alphaBlend: false

- Status: Review
- Priority: Low-Medium
- Category: Online UI / presentation
- Source: The launcher idle-fade cycle's independent review (2026-09-26): repairing that fade established that the theme's shared panel draws pass `alphaBlend: false`, and that the explicit-colour `GUI.DrawTexture` overload hands its colour — and that flag — to the native draw verbatim.
- Related: `review/cuo-launcher-button-obscures-the-view.md` (whose frame took the blended path first), `docs/evidence/selfchecks/ui/cuo-launcher-idle-fade-selfcheck.md`, `docs/evidence/selfchecks/ui/online-ui-panel-blending-selfcheck.md`

## The question

`OnlineUiTheme.DrawBackground(rect)` and `OnlineUiTheme.DrawOverlayBackground(rect)` drew with
`GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, <colour>, 0f, 0f)`.
The fourth argument is `alphaBlend`, and every colour they hand over carries an alpha below 1 —
`Panel` 0.96, `Border` 0.9, `OverlayPanel` 0.58. Unity documents that argument as whether alpha
blending is applied when the image is drawn (enabled by default). The theme's own comments read the
other way: "a dark, translucent 'operator console' look", and for the command console "this remains
translucent so the world stays visible behind the command history". Both cannot be true — but the
native `Graphics::Internal_DrawTexture` body is not inspectable in this repository, so no reading of
the tree can say which of the two the unblended draw produced on screen.

## Decision (2026-09-26 cycle)

**The theme's frames are drawn blended — the flag follows the documented design.** Three facts decide
it without the rendered frame: the palette's alphas and the file's two comments state the intent (a
translucent operator console), the explicit-colour overload is known to hand the flag to the native
draw verbatim (the launcher cycle proved that from the game's own `UnityEngine.IMGUIModule.dll` IL),
and the launcher's own frame already took the blended path. Drawing blended is the one change that is
correct under both readings of the old draw: where it was already blending, nothing changes on screen;
where it was not, the panels become the translucent surfaces the theme documents. The ticket's other
branch — correct the colours to opaque — was rejected because it would make the documented design the
wrong one instead of making the code meet it.

## What landed

The frame draw is the only thing that changed: `OnlineUiTheme.DrawFrame` used to take the flag from its
caller, `DrawBackground(rect)` handed it `false`, and `DrawOverlayBackground` did the same. Every call
site below is byte-identical — what changed is what the helper forwards.

| Surface (call site unchanged) | The theme helper it calls | Forwarded before | Forwards now |
|---|---|---|---|
| The modal Online UI window (`OnlineUiWindow.DrawWindowContents`) | `DrawBackground(rect)` | `DrawFrame(rect, Panel, Border, alphaBlend: false)` | `DrawFrame(rect, Panel, Border)` — blended |
| The launcher (`OnlineUiWindow.DrawLauncherButton`) | `DrawBackground(rect, alpha)` | `DrawFrame(…, alphaBlend: true)` (idle-fade cycle) | unchanged — blended |
| The quick panel (`OnlineUiQuickPanel.Draw`) | `DrawBackground(rect)` | `DrawFrame(rect, Panel, Border, alphaBlend: false)` | `DrawFrame(rect, Panel, Border)` — blended |
| The context menu (`OnlineUiPlayerContextMenu.Draw`) | `DrawBackground(rect)` | `DrawFrame(rect, Panel, Border, alphaBlend: false)` | `DrawFrame(rect, Panel, Border)` — blended |
| The console overlay (`CommandConsoleOverlay`: `Draw`, `DrawClosedNotifications`, `DrawSuggestions`, `DrawTooltip` — four call sites) | `DrawOverlayBackground(rect)` | `GUI.DrawTexture(…, false, 0f, OverlayPanel, …)` | `GUI.DrawTexture(…, true, 0f, OverlayPanel, …)` |

`DrawFrame` no longer takes the flag at all: every themed frame is blended, so a non-blending themed
frame is no longer expressible and the five draws hold the literal `true`. Both `DrawBackground`
overloads keep their signatures, colours and order, so no call site changed; the comments now state the
mechanism instead of the question they used to raise.

## Evidence

- Self-check: `docs/evidence/selfchecks/ui/online-ui-panel-blending-selfcheck.md`
- Pins: `tests/CasualtiesUnknownOnline.Tests/OnlineUi/OnlineUiLauncherFadeTests.cs` — the theme pin
  requires the blended frame draw and the blended console overlay,
  `EveryThemedSurface_DrawsThroughTheBlendedFrame` is the five-surface census, and two mutation
  controls assert the pins reject an unblended frame draw and an unblended overlay; red 4 failed /
  15 passed / 19 on the pre-change `src/` (`%TEMP%/cuo-red-panel-blending.txt`), green 19/19 after it
  (`%TEMP%/cuo-focus-panel-blending.txt`)
- Precedent: `docs/evidence/selfchecks/ui/cuo-launcher-idle-fade-selfcheck.md` (the explicit-colour
  overload, the `GUI.color` finding, the blended launcher frame)

## Limits

- **No rendered pixels here.** Whether the panels were opaque before this change is not observable in
  this repository (the native draw body is not inspectable), and neither is the result: the change
  makes the code request what the theme documents, and the frames themselves are seen in the unified
  acceptance pass.
- The console overlay's 0.58 alpha is now live as its comment describes (the world shows through the
  history, suggestion and tooltip panels). That is the one surface whose appearance this change may
  visibly alter, and its readability over the world is not testable in this tree: it is seen in the
  unified acceptance pass. The modal window's 0.96 and the border's 0.9 are nearly opaque by design, so
  their visual change is subtle even where the old draw suppressed blending.
- The pins read source text: they can prove that every themed surface asks for blending, not what the
  native draw did with either flag.

## Non-goals

- Not a theme redesign; the palette, the fonts and the layout stay as they were.
- Not a new draw path and not a UI-framework change: the two helpers keep their signatures.
