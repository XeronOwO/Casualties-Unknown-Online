# Player-facing text crosses the wire as a key, never as prose

- Status: Todo — opened 2026-10-25 from the user's report, quoted verbatim: "不只是命令,涉及到多语言的都要改".
- Priority: High
- Category: Localization / protocol / player-facing copy
- Source: user report (2026-10-25). The visible case: the cross-player use refusal reached a Chinese client as
  `使用被拒绝：injectable medicine goes through the medical view。` — a Chinese frame (`hud.use_refused` = `使用被拒绝：{0}。`) wrapped
  around an English sentence produced by the host.

## Goal

Every string a player can read is rendered by the client that reads it, in that client's language, as a whole sentence. A key and
its arguments cross the wire; the receiver's catalogue turns them into text. No language's prose ever appears inside another
language's frame, and no English word串 is left in a Chinese surface (or the reverse).

## Current behaviour

- `PlayerItemUseRefusedMsg` carries `Reason` as the host's English sentence, documented as deliberate ("the SAME sentence the host
  logged, not a code the two sides would have to keep in step"), and `PlayerItemUseRefusedHandler` formats `hud.use_refused` with
  it. Sixteen refusal sentences in `PlayerItemUseExecution`, four in `RemoteMedicalOperationHandler` and the CPR refusal all travel
  or surface this way.
- The console's own four dispatcher sentences are already keyed (a previous ticket); host answers to `/heal`, `/give`, `/summon` and
  `/gamerule` still travel as wire sentences.
- `docs/decisions/active.md` records the earlier "prose on the wire" choice; this ticket supersedes it for player-facing text.

## Shape the fix takes

1. A refusal becomes a key plus at most one argument (`ReasonKey` / `ReasonArg`), rendered by the receiver through its own
   catalogue; the host's log formats the same key in English because logs are English.
2. The refusal reasons move into a declared key table whose entries `LocalizationCatalogueGateTests` can see: the census counts a
   `const string` key as a read **only when the constant is referenced**, so a table of keys is legitimate and a dead constant is
   not.
3. The same treatment for every other player-facing string that crosses a boundary, family by family, each with both languages added
   in the same change.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A refusal of each family reaches a Chinese client as a Chinese sentence and an English client as an English one — read from the status line of one real batch, not from the key |
| 2 | No status line, console reply, panel title or tooltip contains a run of the other language's words (a scan over the catalogues and a frame of each surface) |
| 3 | A host-answered command (`/heal`, `/give`, `/summon`, `/gamerule`) reads in the member's language |
| 4 | A key with no text in one of the two catalogues fails the gate (the existing `LocalizationCatalogueGateTests` floor is the pin) |

## Limits

- The keys are a wire change; the protocol version follows decision 241 (pre-release: the frozen baseline does not move, the change
  is recorded by its commit and this ticket).
- Mods may register languages; their strings are their own and out of scope here.
