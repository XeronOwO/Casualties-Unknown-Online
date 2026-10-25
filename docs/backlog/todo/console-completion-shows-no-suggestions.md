# The console's completion shows nothing while a command is typed

- Status: Todo — opened 2026-10-25 from the user's report: "你这命令行补全了个啥,我输命令没有看到补全提示呀".
- Priority: High
- Category: Console / completion / Online UI
- Source: user report (2026-10-25).

## Goal

While the player types a command in the game console, a completion list appears (the resource-id and console-command suggestions the
seam was built for), updates as the text changes, and the accepted suggestion is the canonical id.

## Current behaviour (to be established by the first batch)

Not yet read. What exists: the completion SEAM (`IModResourceCompletion`, `IResourceLocationMatchStage`) survived the pinyin move
into the satellite mod, and `PinyinSearchModConsoleTests.Suggest_ReachesTheCanonicalId_…` pins the suggestion's RESULT for the
satellite mod, but nothing in this repository's acceptance has ever looked at what the console SURFACE draws while the player types.

Two candidate causes, to be told apart before any fix:

1. **The seam is not asked at all** by the console surface in this build (the mod provides the suggester; without the mod installed
   nothing supplies one), or
2. **The suggester answers but nothing draws it** — a UI surface that never renders the list, the same class of defect as the drag
   cue's "state true, nothing on screen".

## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the satellite pinyin mod installed, typing a prefix shows a suggestion list on the screen (a frame), and the list changes as the text changes |
| 2 | Without the mod, typing a name the console knows still shows its own command suggestions (a frame), or the ticket records that the vanilla console has none |
| 3 | Accepting a suggestion (Tab/Enter, in the game's own binding) inserts the canonical id — read from the input field's text and from the executed command's log line |

## Notes on method

The first batch must capture frames while typing: this defect is a "state true, screen empty" candidate, and the user has asked for
frames precisely because a probe answer is not evidence of what the player sees.
