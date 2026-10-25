# The console's completion shows nothing while a command is typed

- Status: Todo — opened 2026-10-25 from the user's report: "你这命令行补全了个啥,我输命令没有看到补全提示呀".
- Priority: High
- Category: Console / completion / Online UI
- Source: user report (2026-10-25).

## Goal

While the player types a command in the game console, a completion list appears (the resource-id and console-command suggestions the
seam was built for), updates as the text changes, and the accepted suggestion is the canonical id.

## The design record this ticket is judged against (it already exists)

The console is **CUO's own Minecraft-style overlay**, designed and accepted in
`docs/backlog/done/in-game-command-console-interactive.md` (category: *Tooling / UI / Minecraft-style command console*; its eleven
derived rows passed in batch `20261018-t`, record `docs/evidence/acceptance/in-game-command-console-interactive-20261018-t.md`).
That design ticket's rejection list is this ticket's specification, quoted: "*Pressing `/` did not pop up the command suggestion
list / autocomplete in the way Minecraft does. The user expects Minecraft-style command suggestions and completion*", and the redo
landed `ConsoleInputSession.LiveSuggestions` ("always reflects the current input, so pressing `/` immediately shows command
candidates and typing narrows them without requiring Tab") plus `CommandConsoleOverlay.DrawSuggestions`. Decision 228 keeps the
in-game `/` overlay as the only command and chat surface.

So this ticket is a **regression against an accepted design**, not a new feature: the sources above exist in the tree, and the
question is why the player sees none of it.

## Current behaviour (to be established by the first batch)

Not yet read on the right surface. A batch on 2026-10-25 opened the GAME's native console (the console key, `ConsoleScript`) and
captured two frames — the wrong surface for this ticket, recorded as a failed batch: the log panel was visible, no input box was,
and no suggestions appeared. The design ticket above names the real surface (`CommandConsoleOverlay`, opened by `/`), and the batch
did not manage to open it through OS input (`Command console opened by …` never appeared in the host's log), so the missing
capability for the next batch is **a reliable way to open CUO's overlay** — a driver seam or a key path that the overlay's own
OnGUI receives — before any row here can be judged.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | With the satellite pinyin mod installed, typing a prefix shows a suggestion list on the screen (a frame), and the list changes as the text changes |
| 2 | Without the mod, typing a name the console knows still shows its own command suggestions (a frame), or the ticket records that the vanilla console has none |
| 3 | Accepting a suggestion (Tab/Enter, in the game's own binding) inserts the canonical id — read from the input field's text and from the executed command's log line |

## Notes on method

The first batch must capture frames while typing: this defect is a "state true, screen empty" candidate, and the user has asked for
frames precisely because a probe answer is not evidence of what the player sees.
