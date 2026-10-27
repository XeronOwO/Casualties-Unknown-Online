# Player-facing text crosses the wire as a key, never as prose

- Status: Review — code landed 2026-10-25 (`91f64718`, `6d9b7732`, `1a12bcd5`, `93f559a9`, `ce4a7d51`, `c940d45f`); rows 1 and 3
  wait for a real two-client batch.
- Priority: High
- Category: Localization / protocol / player-facing copy
- Source: user report (2026-10-25). The visible case: the cross-player use refusal reached a Chinese client as the Chinese refusal frame
  (`hud.use_refused`, which reads "use refused: {0}.") wrapped around an English sentence produced by the host — a mixed-language line,
  which is the defect this ticket is about.

## Goal

Every string a player can read is rendered by the client that reads it, in that client's language, as a whole sentence. A key and
its arguments cross the wire; the receiver's catalogue turns them into text. No language's prose ever appears inside another
language's frame, and no English fragment is left in a Chinese surface (or the reverse).

## Where the code stands

Every player-facing string that crosses a boundary travels as a KEY plus its values, and every one of them is rendered by the client
that shows it:

- **Refusals that cross**: `PlayerItemUseRefusedMsg` carries `ReasonKey` + `ReasonArg`, rendered by the member's own catalogue as ONE
  whole sentence — the reported case cannot recur. `hud.use_refused` is gone with its last caller.
- **Refusals decided locally** (never on the wire, but the reader's language still has to win): the medical view's four and the CPR
  gate's two, each through its own key-producing helper (`RefuseLimbUse`, `RefuseCpr`).
- **Command answers**: `/heal`, `/give`, `/summon` and `/gamerule` all answer as catalogue PARTS (`ConsoleAnswerPartMsg`: a key plus
  its ordered arguments), rendered one line per part by the MEMBER's console in the member's language. The plain text stays on the
  result messages as the host's log line and as the fallback a message with no parts degrades to.
- **A ground another module owns** (a give parser's usage line, the creation port, the custody path, the layer stamp, the prefab
  parse) is answered with one catalogue sentence (`answer.refused_see_host_log`) and the detail stays in the host's log: wrapping
  another module's English in a localized frame is the defect this ticket exists for, and a port that later returns its own key is
  promoted to a real part without changing the shape.
- **The catalogues are scanned**: `LocalizationCatalogueGateTests.TheEnglishTable_HoldsNoChineseText` and
  `…TheChineseTable_HoldsNoEnglishProse` fail on the other language's text, naming the key; one or two ASCII tokens inside a Chinese
  sentence (a command, an identifier, a product name) are allowed because the game's own Chinese UI writes them that way.
- **The census follows the new shapes**: an accessor call whose key argument is a stored `.Key`, a helper's `labelKey`, a keyed
  answer part or a `RefuseByRule`/`AnswerPart` call site is a READ, so a key carried as a value is not reported as an orphan.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 0 | A console COMPLETION hint is a typed fact the reader's catalogue renders: `heal` and `help` read in Chinese on a Chinese client | **judged 2026-10-25** by `.acceptance/frames/console-zh.png` — the two hint rows read in Chinese (translated: "immediately fully restore the selected player(s) … (a cheat verb)" and "list available commands, or show one command's usage"), where they used to read in English (the user's report) |
| 1 | A refusal of each family reaches a Chinese client as a Chinese sentence and an English client as an English one — read from the status line of one real batch, not from the key | **owed**: needs a two-client batch |
| 2 | No status line, console reply, panel title or tooltip contains a run of the other language's words | **met by the gate** for the catalogues (`c940d45f`); a frame of each surface is owed with row 1 |
| 3 | A host-answered command (`/heal`, `/give`, `/summon`, `/gamerule`) reads in the member's language | **PASS 2026-10-27** (same batch): on the English member each family answered in English — `/heal` → "76561198863287957 fully reset your state." + "Reset …: sent to that player's own client, which applies the full reset and re-reports the state."; `/give` → "Gave dogfood (#1314580973507) to … in slot 4."; `/summon` → the English usage refusal; `/gamerule` → the English unknown-rule refusal with its known list |
| 4 | A key with no text in one of the two catalogues fails the gate | **met**: `LocalizationCatalogueGateTests` floor |

## What blocks rows 1 and 3

One real session with a Chinese client and an English client: `/heal`, `/give`, `/summon` and `/gamerule` issued from the member
against the host, plus one cross-player refusal, each read off the member's console — the part no test host can produce. Until that
batch runs, the rows are unjudged rather than passed.

## The frame that judged row 0 also found the next gap (2026-10-25)

`.acceptance/frames/console-zh.png` shows the completion hints in Chinese AND, one line above them, a console line still in English
prose: "CUO new player: the world's own first-layer supplies (light) are already yours — CUO granted nothing on top." That is the
ANSWER half of the console family (a text line rather than a keyed part), which is the same defect row 0 closed for completions.
It is recorded here rather than fixed in the localisation commit because the answer path (`ConsoleAnswer` text lines) needs the
keyed-part shape the four host commands already use, which is a different change.

**Fixed 2026-10-26**: that line now travels as a KEY plus its arguments — `StartingSupplyGrantReport.LineKey()` / `LineArgs` (one key per
disposition shape, five of them) printed through `CommandConsoleService.AddKeyedLine`, so the console renders it in the reader's language while
`Describe()` stays as the host's log line and as the fallback a catalogue gap degrades to. Five keys in both catalogues; `LineKey` is declared in the
localisation census so a new disposition has to carry a key.

The game's own console prompt ("Enter command (type help for command list)") is the game's prefab text, not CUO's: it is absent from
`src/` and from `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs`, whose input field carries a scene-set placeholder.

## What it was before the fix (the record, not the current state)

- `PlayerItemUseRefusedMsg` carried `Reason` as the host's English sentence, documented as deliberate ("the SAME sentence the host
  logged, not a code the two sides would have to keep in step"), and `PlayerItemUseRefusedHandler` formatted `hud.use_refused` with
  it. Sixteen refusal sentences in `PlayerItemUseExecution`, four in `RemoteMedicalOperationHandler` and the CPR refusal all travelled
  or surfaced this way.
- The console's own four dispatcher sentences were already keyed (a previous ticket); host answers to `/heal`, `/give`, `/summon` and
  `/gamerule` travelled as wire sentences.
- `docs/decisions/active.md` records the earlier "prose on the wire" choice; this ticket supersedes it for player-facing text.

## Limits

- The keys were a wire change; the protocol version follows decision 241 (pre-release: the frozen baseline does not move, the change
  is recorded by its commits and this ticket).
- Mods may register languages; their strings are their own and out of scope here.

## The census behind the failing rows (2026-10-27)

`ConsoleAnswer.*` has **124 call sites under `src/`**, and **42 of them build an interpolated raw English sentence** (`ConsoleAnswer.Refused($"…")` and
siblings). Those 42 are the family row 1 fails on: each names a fact (an unknown member, a rule, an item, a position) and each is read by a client that may
have the other language. The ticket''s next work item is the same shape the five starting-supply keys already use — one key per answer with its arguments in
`en.json`/`zh.json` — and the census above is the work list, not a sample.

## Two families now read Chinese on a Chinese client (2026-10-27, frames)

| Family | Before | After (captured frame) |
|---|---|---|
| `/gamerule` unknown rule | the English sentence `Unknown game rule 'duelSpawn'. Known rules: …` | the Chinese catalogue's sentence for `rules.answer.unknown`, with the rule id and the known-rule list as its arguments |
| the cheat gate's three refusals | the English sentence `No active session — a cheat verb needs one: …` | the Chinese catalogue's sentence for `cheat.refused.no_session`, with the verb as its argument |

Both frames are the console overlay of a `Language = zh` client, read back image by image. Identifiers (`/give`, the rule ids, the command names) stay as
they are; only the sentence shell is translated, which is what "the reader's language" means here.

Two causes had to fall, and the second one is the lesson: the answers had no key, AND `ConsoleLineText.Resolve` resolved only a switch over six
`console.*` literals — so even a keyed answer printed English.

What is left of row 1 is the raw-answer census the gate budgets (42 at the last count), one family per commit.

## `/help <command>` and the caret, verified on frames (2026-10-27)

- `/help clear` on a `Language = zh` client answers the description from `console.cmd.clear.desc` (the Chinese catalogue's own sentence) and keeps the usage line
  as syntax (`Usage: /clear`); the completion hint row under it says the same sentence, because both read `CommandDescriptions`.
- The input row''s caret now sits immediately after the last glyph in a captured frame (three frames taken to catch the blink phase); the stylus the
  glyphs are drawn with is the one every measurement uses.