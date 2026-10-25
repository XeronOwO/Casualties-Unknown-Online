# Localization: one language per reader, whole sentences

Every string a player reads is rendered by the client that reads it, in that client's language, as one whole sentence.

## The rule

- A player-visible string is never prose that crosses a boundary. What crosses is a **key** plus the values the sentence
  needs; the receiving client's catalogue turns the pair into a sentence.
- A localized frame never wraps another language's prose: a sentence in English inside a Chinese frame, or the reverse,
  is the defect this rule forbids.
- A surface with no catalogue entry is not shipped with raw text: a missing entry means the design is not finished, and
  the catalogue gate refuses the commit while one catalogue lacks a key the other has.
- **The product's own name is localized.** The window title uses the game's official name in the reading language: the accepted reading is that "the Chinese title reads … ; the English one is unchanged", so a product name is a catalogue entry rather than a hard-coded string.- The same sentence is never written twice: one condition has one key, shared by the client that decided it and the
  client that reads it.

## Where the catalogue comes from

The UI's text is an embedded JSON data file per language with English as the fallback; a strict loader reads it, the gates read the data rather than the code, and a mod adds a language through `RegisterCatalog` — the accepted record's words: "the UI's text is an embedded JSON data file per language (English as the fallback), the catalogue is read by a strict loader, the gates read the data, a mod registers a language through `RegisterCatalog` and the picker lists it". The deployed client's own service was read afterwards: the host answered `zh` with its own word for the page and the guest answered the same key in English.

## Where strings live

| Kind of string | Where it is rendered |
|---|---|
| console answers, refusals and status lines | the reading client, from its own catalogue |
| panel titles, labels, buttons | the client drawing the surface |
| command semantics and their arguments | the console surface's own vocabulary |
| log lines | the host's log is a diagnostic channel, always in one language, and never a player-facing surface |

## The console's own prose

The console builds sentences of its own, and they follow the same rule. The accepted record lists the keys it now carries — `console.ready`, `console.session_ended`, `console.command.host_only`, `console.command.empty`, `console.command.unknown`, `console.chat.not_sent` — and its live reading is that the host's console buffer holds a line keyed `console.ready` while its service answers Chinese and the guest's the same key with English, with the launcher drawing `CUO 联机` on one client and `CUO ONLINE` on the other. A keyed line is resolved through the same catalogue read the gate counts, and a pin fails the build if a catalogued console key is never resolved.

## Who judges

The side that produced a sentence renders nothing; the side that READS it renders it. So a refusal is judged where the action landed, and its sentence is composed by the client that displays it.

## What travels instead of text

- A refusal or an answer travels as a key plus at most the values its sentence names.
- A name the player typed (a persona, a world name) travels as text and is shown as it was written; it is never
  translated.
- A formatted number, a distance or an amount travels as a number and is formatted by the reader.

## How a player experiences it

The same action reads naturally in both languages: the operator's screen, the target's screen and the host's console each
show their own language for the same event, and no screen mixes the two.

## Records

`../../decisions/active.md` (the console's answer family), `../../architecture/surfaces.md` (status lines row), and the
catalogue gate named in `../../evidence/normative-gates.md`.

## Gaps

- The refusal sentences a cross-player use produces still travel as prose on one path; recorded in
  `../../backlog/todo/player-facing-text-localized-per-receiver.md`.
