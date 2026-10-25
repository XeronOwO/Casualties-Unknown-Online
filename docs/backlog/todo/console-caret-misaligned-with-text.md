# The console input's caret does not line up with the text it edits

- Status: Todo — opened 2026-10-25 from the user's report: "输入的时候,文本光标跟里面的字对不齐", with the user's own guess: "我能看到你用圆角矩形把字框起来了,会不会是这个问题".
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

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A frame taken mid-typing shows the caret touching the last glyph's right edge on the same baseline (both languages, ASCII and CJK) |
| 2 | The same holds with the text longer than the field (scrolled to the end): the caret stays at the last glyph, not at the frame's edge |
| 3 | The frame's own inset matches the text's: the first glyph's left edge equals the caret's position with an empty field |

## Notes on method

Frames, not probes: the defect is a pixel fact, and the user's report came from looking at the screen.
