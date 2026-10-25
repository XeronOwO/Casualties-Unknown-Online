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

## What landed (2026-10-25, `91f64718`)

The REFUSAL family is done end to end, and it was the case the user reported:

- `PlayerItemUseRefusedMsg` carries `ReasonKey` + `ReasonArg` instead of the host's sentence, and
  `PlayerItemUseRefusedHandler` renders the key with the member's own catalogue — one whole sentence in the reader's language,
  with no frame wrapped around a foreign clause. `hud.use_refused` is gone with its last caller.
- The executor's fifteen refusal sentences became keys produced by one `RefuseItemUse` helper, which also logs (Debug) the
  English sentence the same key resolves to, so the host's log stays diagnosable without a second copy of the wording.
- The LOCAL refusals that never cross a client got the same treatment, each with its own key-producing helper
  (`RefuseLimbUse`, `RefuseCpr`), because the operator who reads them is the one whose language must win.
- Both catalogues carry the eighteen `refusal.*` keys, added in the same change (the gate's floor).
- Two census improvements followed from the shape: an accessor call whose key argument is a stored `.Key` or a helper's
  `labelKey` is now RESOLVED rather than unresolvable — carrying a key as a value was already counted at its producer, and a
  scanner that read the new shape as a failure would forbid the fix.

## What remains

- **The other two answer paths.** `/heal` now carries parts (`6d9b7732`). `/give` and `/summon` use the same shape
  (`PlayerInventoryGiveResultMsg`, `WorldSummonResultMsg`) and need the same field plus their own keys; until then their answers
  fall back to the host's text, which is the deliberate degradation the console service implements.
  **The vocabulary, so the next cycle does not re-derive it:** `/give` answers "Created {definition} (id {instance}) for {player}
  in slot {slot}." / "…dropped at that player's position because nothing accepted it." / the transfer refusals; `/summon` answers
  "Summoned {prefab} at ({x}, {y})." plus the same refusal family the summon service already names.
- **`/gamerule`'s answer** is a list of setting names and values and takes the same parts shape.
- **The scan in acceptance row 2** (no run of the other language's words in any catalogue) is a gate to write once the remaining
  families are keyed, so it does not fail on text this ticket has not converted yet.

## What it was before the fix (the record, not the current state)

- `PlayerItemUseRefusedMsg` carried `Reason` as the host's English sentence, documented as deliberate ("the SAME sentence the host
  logged, not a code the two sides would have to keep in step"), and `PlayerItemUseRefusedHandler` formatted `hud.use_refused` with
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
