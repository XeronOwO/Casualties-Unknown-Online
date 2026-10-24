# The console's chat-box messages are English only

- Status: Todo — opened 2026-10-25 by the user's report, clarified the same minute: the bilingual switching they want is the
  **hint/notice text the console prints into its chat box**, not the commands themselves. Command names and syntax stay
  English (`/heal`, `/op` and their arguments are the language of the console, as in Minecraft); what a Chinese player must
  read in Chinese is the console's own prose — refusals, results, hints and suggestions.
- Priority: Medium
- Category: Localization / console surface
- Source: the user, while a batch's console replies were on screen ("Refused locally: the host's cheat roster … does not
  name you …" and "Granted the cheat verbs to …"). The Online UI is bilingual through `LocalizationCatalog`; the command
  console's replies are hard-coded English in the Runtime services that produce them.

## What is wrong today

Every console line a player reads is built as an interpolated English string inside the Runtime service that decides it —
`CheatVerbGate` ("Refused locally: the host's cheat roster … does not name you, so /heal cannot land. The host grants it
with /op."), `CheatAdminCommands` ("Granted the cheat verbs to {label}; the roster is announced to every member."),
`PlayerHealResetService`, `PlayerItemGiveService`, `WorldSummonService`, `HostRuleChangeService`, and the refusal reasons
in `ItemDataWriteRefusal`. The UI's own strings are keys into `LocalizationCatalog` (English + Chinese), so a Chinese
player sees a Chinese window and an English chat box.

**Scope boundary (the user's clarification):** the command's own spelling is not translated. `/heal @s` stays `/heal @s`,
its arguments stay English, and the completion menu keeps offering the English verbs — that is the console's syntax, and it
matches the template this project follows. Only the sentences the console PRINTS are localized.

## Design (the layering that fixes it without putting UI language into Runtime)

Every player-facing line already funnels through ONE method — `CommandConsoleService.AddLine(string text, kind)` — so the
change lands at that funnel and nowhere else:

1. **A console line may carry a message key and its arguments** beside its text: `ConsoleLine` gains optional `Key` + `Args`,
   `AddLine` gains an overload that takes them, and the English `Text` is still produced for the same line so the log, the
   history search and any client whose language lacks the entry keep reading a sentence. An echoed command line is the
   player's own input: it never carries a key and is never translated.
2. **The catalogue owns the sentence**, in both languages, under a `console.` prefix — the same `LocalizationCatalog` the
   window uses, so a player's language setting moves both surfaces at once.
3. **The console overlay renders the key** through the localization service when the client's language has it, and prints the
   line's `Text` otherwise (a missing key must never blank a line).
4. **Emission sites convert per family**, not in one sweep: the roster/heal/give/summon refusals first (they are what a player
   hits), then the admin replies, then the rest. Each conversion is mechanical and keeps its English text in place.
5. **The console's syntax does not change**: verbs, arguments and the completion menu stay English, as the user clarified.

## Run sheet

- [x] **Step 1 — the line model.** `ConsoleLine` carries an optional key and arguments beside its English text; every existing call still compiles through the `Text` form.
- [x] **Step 2 — the catalogue.** `console.ready` and `console.session_ended` in English and Chinese, in the embedded JSON files the UI already reads.
- [x] **Step 3 — the overlay.** `ConsoleLineText.Resolve` renders a keyed line through the localization service (every key is a literal `T("console.…")` call, so the catalogue gate sees the reader), and an unkeyed line keeps its own text.
- [ ] **Step 4 — the emission sites.** DONE for the two lines every session prints; the families still English are the roster/heal/give/summon refusals and the admin replies, which surface through `ConsoleAnswer`/`CheatVerbGate.Verdict` sentences rather than the console service's own `AddKeyedLine`, so each family needs its answer type to carry the key it already knows the ground for.
- [ ] **Step 5 — verify on the machine.** Deployed with the localization change (artifact `0.1.0+3ec84ae0…`); the reading — one refusal and one grant in each client's own language — is still owed, and it is the same UI-text read the localization ticket's step 7 names.
- [ ] **Step 6 — tests, gates, deploy, record.** The catalogue gates are green with the two keys; a unit case per converted emission is only meaningful once the answer types carry keys (step 4).

## Acceptance this ticket owes

The console speaks the client's language: a refusal and a grant read in Chinese on a Chinese client and in English on an
English client, from the identical action, with the fallback proven by a key that has no entry (the line still prints its
text).
