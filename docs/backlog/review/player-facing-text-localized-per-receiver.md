# Player-facing text crosses the wire as a key, never as prose

- Status: Review — code landed 2026-10-25 (`91f64718`, `6d9b7732`, `1a12bcd5`, `93f559a9`, `ce4a7d51`, `c940d45f`); rows 1 and 3
  wait for a real two-client batch.
- Priority: High
- Category: Localization / protocol / player-facing copy
- Source: user report (2026-10-25). The visible case: the cross-player use refusal reached a Chinese client as
  `使用被拒绝：injectable medicine goes through the medical view。` — a Chinese frame (`hud.use_refused` = `使用被拒绝：{0}。`) wrapped
  around an English sentence produced by the host.

## Goal

Every string a player can read is rendered by the client that reads it, in that client's language, as a whole sentence. A key and
its arguments cross the wire; the receiver's catalogue turns them into text. No language's prose ever appears inside another
language's frame, and no English word串 is left in a Chinese surface (or the reverse).

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
| 1 | A refusal of each family reaches a Chinese client as a Chinese sentence and an English client as an English one — read from the status line of one real batch, not from the key | **owed**: needs a two-client batch |
| 2 | No status line, console reply, panel title or tooltip contains a run of the other language's words | **met by the gate** for the catalogues (`c940d45f`); a frame of each surface is owed with row 1 |
| 3 | A host-answered command (`/heal`, `/give`, `/summon`, `/gamerule`) reads in the member's language | **owed**: same batch |
| 4 | A key with no text in one of the two catalogues fails the gate | **met**: `LocalizationCatalogueGateTests` floor |

## What blocks rows 1 and 3

One real session with a Chinese client and an English client: `/heal`, `/give`, `/summon` and `/gamerule` issued from the member
against the host, plus one cross-player refusal, each read off the member's console — the part no test host can produce. Until that
batch runs, the rows are unjudged rather than passed.

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
