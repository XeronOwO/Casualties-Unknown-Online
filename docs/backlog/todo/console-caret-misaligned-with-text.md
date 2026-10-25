# The console input's caret does not line up with the text it edits

- Status: Todo — opened 2026-10-25 from the user's report (translated): "when I type, the text caret does not line up with the characters",
  with the user's own guess (translated): "I can see you frame the characters with a rounded rectangle — could that be the cause?".
- Priority: Medium
- Category: Console / text input / Online UI
- Source: user report (2026-10-25).

## Goal

The caret sits exactly where the next character will go: same baseline, same left inset, at the end of the text. While typing, the
visible text and the caret never disagree, in either language and at any text length.

## The design record this ticket is judged against

The console is CUO's own Minecraft-style overlay (`docs/backlog/done/in-game-command-console-interactive.md`, accepted in batch
`20261018-t`), whose rejection list already names this exact defect: "*The input line has layout/alignment problems: the `>` prompt and
the input box are misaligned*". The redo claimed "the input prompt and field are aligned in one manual input row" — so this ticket is
a **regression against that accepted design**, on `CommandConsoleOverlay`'s manual input row (IMGUI), not on the game's native
console.

## Current behaviour (to be established by the first batch)

Not yet read. The console's input is drawn by CUO's own surface, where the text field sits inside a framed (rounded) background — the
user's hypothesis is that the frame's inset or its padding is what pushes the glyphs and the caret apart.

Candidates to tell apart with one frame plus two readings:

1. **The frame's padding differs from the text field's inset** — the glyphs are drawn inside a narrower rect than the caret's
   position, so the offset grows with the caret index.
2. **Two different rects are edited and drawn** — the field measures/scrolls in one rect and the caret uses the other (a TMP
   `textComponent`/`caret` mismatch, or a viewport that is not the frame's inner rect).
3. **Scale**: the frame is drawn at a canvas scale the text does not use, so the offset is proportional to the distance from the
   pivot.

## Root cause and fix (2026-10-25)

Candidate 1 of the three, in a stronger form than "different insets": the segments were drawn with the **text-field style itself**.

`CommandConsoleInputRenderer` measures and draws the input token by token (`DrawHighlightedInput` → `DrawSegment(rect, x, style, …)`), and the
style it passed was `InputStyle()` — a clone of `GUI.skin.textField` with `padding = new RectOffset(6, 6, 4, 4)`. A `GUIStyle` with padding draws
its text INSET by that padding inside the rect it is given, while the loop already placed each token at `rect.x + style.padding.left + Σ widths`:

- every token's glyphs therefore landed one padding further right than the x computed for them, and the drift grew with every token;
- the caret measured the same Σ widths without the insets, so it fell further behind the text the longer the line got — the reported symptom;
- and the text-field style's own background drew a **rounded rectangle around every token**, which is the frame the user guessed at
  ("could the rounded rectangle be the cause?").

That last half is the defect the earlier accepted design's rejection list named ("the input line has layout/alignment problems: the `>` prompt and
the input box are misaligned"), so this is the regression it describes.

**The fix** is one style used for BOTH measuring and drawing (`SegmentStyle()`): the input style with `padding` zeroed and every
`background` cleared. One origin (`rect.x + style.padding.left`), zero padding, no per-token frame — the text and the caret cannot disagree, and the
row keeps the single frame it already drew.

## Judged (2026-10-25, `.acceptance/batch-caret2`)

The row was judged by capturing the client's own frame with the text in the field, twice — before the background half of the fix and after:

- **before** (`.acceptance/frames/caret-fix.png`): three rounded boxes, one per token (`/give`, `waterbottle`, `@a`);
- **after** (`.acceptance/frames/caret-fixed2.png`): one text run `/give waterbottle @a`, no per-token frames, and the caret is a thin bar at the
  end of `@a` on the same baseline — exactly where the next character goes.

**Residual, not judged:** the CJK half of row 1. The in-process driver cannot deliver Chinese text — `-Text '/give 水壶 @a'` arrives as the
misdecoded `姘村６`, a transport artifact of the batch (PowerShell → JSON → eval), not of the console. Judging the caret after an IME composition
needs the user's own keyboard, so it stays a named residual.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A frame taken mid-typing shows the caret touching the last glyph's right edge on the same baseline (both languages, ASCII and CJK) |
| 2 | The same holds with the text longer than the field (scrolled to the end): the caret stays at the last glyph, not at the frame's edge |
| 3 | The frame's own inset matches the text's: the first glyph's left edge equals the caret's position with an empty field |

## Notes on method

Frames, not probes: the defect is a pixel fact, and the user's report came from looking at the screen.
