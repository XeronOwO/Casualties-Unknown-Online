# The player context menu's title wraps and hides behind its first action

- Status: Todo — opened 2026-10-25 from the user's report: "没有硬切,是换行!!!在医疗按钮背面", and "你没适配玩家名字长度".
- Priority: Medium
- Category: Online UI / panel layout
- Source: user report (2026-10-25), seen on the host's screen with the guest's name `Kaguya（小号）`.

## Goal

A player's display name in the right-click context menu's title bar stays on ONE line and stays readable: the panel widens to fit
the measured name up to a cap, and a name past that cap ends in an ellipsis. Nothing of the title may be drawn under, behind or
between the action buttons.

## Current behaviour (read from a captured frame, not from a probe)

The menu's title bar shows `Kaguya（小`, and the rest of the name wraps onto a second line that the first action button (`医疗`)
covers. The panel's width comes from the model (`OnlineUiPanelModel.Width`) and the title label is a `TextMeshProUGUI` created with
TMP's default word wrapping on, so the name wraps inside a fixed-width title bar whose height is a single line
(`OnlineUiPanelView.TitleHeight`).

Measured on the machine 2026-10-25 with `open-context-menu target=76561198863287957 x=820 y=430` followed by `capture-frame` on the
host; the frame is the evidence and the batch record names it.

## Shape the fix takes

1. The title label is single-line: `enableWordWrapping = false` and `overflowMode = Ellipsis`.
2. The panel's effective width is the larger of the model's width and the title's MEASURED preferred width plus padding, capped
   (`MaxTitleFitWidth`), so the common long name fits and an extreme one ellipsizes.
3. The rows keep their column rule: only the panel's outer width changes, never a row's internal layout.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The default name `Kaguya（小号）` is drawn whole on one line, and a frame shows it inside the title bar with no second line under the first action |
| 2 | A name longer than the cap ends in `…` inside one line, and the action buttons are not moved or covered |
| 3 | The quick panel (the other `OnlineUiPanelModel`) keeps its fixed width because its title is a key, not a name |

## Limits

- The measurement happens in the adapter, where TMP and the font live; the runtime model stays a plain value.
- A frame is required for every row: the defect was invisible in the probe readings, which reported the menu "opened" and the
  actions correct.
