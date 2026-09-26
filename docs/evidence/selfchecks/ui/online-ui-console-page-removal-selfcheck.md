# The Online UI window's console page is removed — self-check (2026-09-26)

Ticket: `docs/backlog/review/remove-the-online-ui-console-page.md`. Cycle scope: delete the window's
duplicated console page end to end — the tab, the page enum member, the drawer, the window state it
used and the catalogue keys that only it read — while the in-game `/` overlay stays the only command
and chat surface (user ruling 2026-09-21, decision 228).

## 1. Mechanism inventory

| # | Mechanism | Evidence | Change | Evidence (ours) |
|---|---|---|---|---|
| 1 | The window's page set: `OnlineUiPage`, one tab per page in `OnlineUiWindow.DrawTabs`, one `switch` case per page | the three files as they stood at HEAD | the console member, its tab and its case are deleted; six pages remain | `OnlineUiConsolePageRemovalPinTests` — the enum body, the tab row and the switch body are compared exactly, and the file must draw exactly six tabs anywhere in it |
| 2 | `OnlineUiConsoleDrawer` projected the same `CommandConsoleService` buffer through its own text field and `TryExecute` call | the deleted file (`ctx.Commands.Lines`, `ctx.Commands.TryExecute(input)`) | the file is deleted | the pin's plugin-source scan (`OnlineUiConsoleDrawer` in no plugin file) + the command-buffer census (only the overlay reads `ctx.Commands`) |
| 3 | The overlay's own surface: `CommandConsoleOverlay` + `ConsoleInputSession` + `CommandConsoleInputRenderer`, opened with `/` by `OnlineUiHost` | `CommandConsoleOverlay.cs` reads `console.overlay.empty` | unchanged | the pin's key census keeps `console.overlay.empty` in the required set, so losing its reader fails |
| 4 | `OnlineUiWindowState.ConsoleInput` — the page's input buffer | its only reader and writer was the drawer (repo-wide census) | deleted | the pin's word-bounded window-state scan |
| 5 | The catalogue keys the page read | `tab.console`, `console.title`, `console.send` — read by the window and the drawer alone | deleted from both dictionaries | the pin's declared-key census, both languages |
| 6 | Two keys nothing referenced any more | `console.hint`, `console.overlay.controls` — the repo-wide census found no reader at HEAD | deleted with the same family | the pin's declared-key census; `LocalizationServiceTests.LegacyInventoryExpansionKeysAreRemoved` is the precedent for pinning a removed key |
| 7 | The overlay's remaining key | `console.overlay.empty` in `CommandConsoleOverlay.cs` | kept, and required to stay read | the same census (`BothLanguagesCarryEverySurvivingKey`) |

The census method: scan every source file under `src/` (build output excluded) for the read shape
`T("…")` whose key matches `(?:tab|console)\.[a-zA-Z_.]+`, strip comments — line AND block, with
string and character literals preserved — and compare that set with the catalogue's declared keys.
HEAD's referenced set was ten keys (seven `tab.` plus three `console.`), its declared set twelve, and
the difference is exactly the two orphans in item 6. Counting READS rather than textual mentions is
deliberate: the review round showed that a mention in a comment or a log literal would otherwise
stand in for a deleted read.

## 2. What decides it

The user ruled (2026-09-21) that the window's console page is deleted rather than kept in sync beside
the in-game command line, and declined a replacement button in the window: the `/` overlay is the
same surface (one `CommandConsoleService`) with more capability — completion, history, suggestions
and the notification lines. This cycle executes that ruling; it takes no new design decision, and no
work-item question was asked.

## 3. Whole-family audit

| Family member | Verdict | Reason |
|---|---|---|
| The window's tab row | CHANGED — six tabs | one tab per surviving page, in enum order; a seventh tab anywhere in the file fails the pin |
| The window's page switch | CHANGED — six cases | the same set: a case without a member, or a member without a case, fails |
| The page enum | CHANGED — six members | it is the census both the tab row and the switch are compared against |
| The console drawer | DELETED | it owned no capability the overlay lacks; its file, type and state are gone |
| The window state | CHANGED | `ConsoleInput` deleted with its only reader and writer |
| The catalogue's `tab.`/`console.` keys | CHANGED | exactly the keys the source reads, in both languages |
| The command buffer (`ctx.Commands`) | NARROWED to the overlay | a new fact requires the overlay to be its only reader, so an inline panel in the window cannot replace the page quietly |
| The overlay: renderer, input session, hotkey, notification lines | UNCHANGED | non-goal; its key stays in the required set |
| The command and chat service | UNCHANGED | one service served both surfaces; nothing about it is touched |
| The launcher button and the Home page | UNCHANGED | their `OnlineUiPage.Home`/`Players` logic is not touched |
| Wire protocol, saves, host rules | UNCHANGED | the page was local presentation only |

## 4. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| The page enum | six members, exact body | the pin's enum fact + its re-added-member sample |
| The tab row | one tab per page, exact body, six tabs in the file | the pin's tab fact + its re-added-tab, dropped-tab and seventh-tab samples |
| The page switch | one case per page, exact body | the pin's switch fact + its re-added-case sample |
| The drawer | file deleted, type absent from the plugin source | the pin's file-existence and source-scan assertions + its re-added-reference sample |
| The command buffer | read by the overlay alone | the pin's file census + its inline-panel sample |
| The window state | the input field deleted, the session type tolerated | the pin's window-state fact (word-bounded) + its re-added-field and session-tolerance samples |
| The key census | declared == read, both languages | the pin's census fact + its orphan-key, dangling-reference, block-comment-key and log-literal-key samples |
| The overlay's key | still declared and still read | the same census + `BothLanguagesCarryEverySurvivingKey` |

## 5. Red → green record

The red was produced by writing the five files back to HEAD's text (the four modified ones from the
backup the cycle took first, the deleted drawer from `git show HEAD:…`), running the pin, and
restoring the working shapes immediately afterwards.

- **Red** on the HEAD-shaped tree with the DELIVERED pin: **8 failed / 13 passed / 21**, exit 1
  (`%TEMP%/cuo-red2-online-ui-console.txt`). The eight failures are the contract itself — the enum
  census, the tab row, the switch, the drawer trace, the command-buffer census, the window-state
  field and the key census — plus the window-state tolerance sample, which can only hold once the
  field is gone. The thirteen passes are the twelve negative samples (matcher self-tests, which hold
  on both trees by design) plus `BothLanguagesCarryEverySurvivingKey`, which holds at HEAD as well
  because the overlay's own key was never at issue.
- **Green** after the restore: **21 passed / 0 failed**, exit 0
  (`%TEMP%/cuo-focus6-online-ui-console.txt`).
- The pre-review pin (15 cases, before the review round widened the matchers) recorded
  **6 failed / 9 passed / 15** on the same tree (`%TEMP%/cuo-red-online-ui-console.txt`); the
  delivered pin's record above is the one that describes this artifact.
- What the red tree contained, exactly: HEAD's `OnlineUiPage.cs`, `OnlineUiWindow.cs`,
  `OnlineUiWindowState.cs`, `LocalizationCatalog.cs` and `OnlineUiConsoleDrawer.cs`, with the new pin
  and the rest of the working tree (including the delivery-checklist reset) left in place — so no
  failure can be attributed to anything but the reverted shapes.
- Three compile errors and two polarity defects surfaced while landing the pin — a `MatchCollection`
  that needs `Cast<Match>()` before `Select`, and detector predicates inverted (caught by the focused
  runs) — all fixed before the record above was taken; the build is 0 warnings / 0 errors.

## 6. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The pin passes on the delivered tree | `dotnet test … --filter "FullyQualifiedName~OnlineUiConsolePageRemovalPinTests"` | 21 passed / 0 failed, exit 0 (`%TEMP%/cuo-focus6-online-ui-console.txt`) |
| The Online UI, localisation, command-console and input-session families did not regress | `--filter "FullyQualifiedName~OnlineUi\|FullyQualifiedName~Localization\|FullyQualifiedName~CommandConsole\|FullyQualifiedName~ConsoleInput"` | 178 passed / 0 failed (`%TEMP%/cuo-family2-online-ui-console.txt`) |
| The repository gates hold | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --filter "FullyQualifiedName!~DeliveryChecklist"` | 208 passed / 0 failed while this cycle's checklist is reset; the unfiltered run before the commit is below |
| The build is clean | `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors (`%TEMP%/cuo-build4-online-ui-console.txt`) |
| Formatting is clean | `dotnet format CasualtiesUnknownOnline.slnx` with its exit code captured and the tree compared before and after | exit 0, `git status --short` identical before and after — no file changed (`%TEMP%/cuo-format2-online-ui-console.txt`, `%TEMP%/cuo-status-before-format.txt` / `-after-format.txt`) |
| The whole suite runs green with the checklist checked | `dotnet test CasualtiesUnknownOnline.slnx` (with build), unfiltered | main 4075 passed + gates 209 passed, exit 0 (`%TEMP%/cuo-full4-online-ui-console.txt`); the first unfiltered attempt was red on the checklist gate, which caught one box whose continuation line had been given its evidence without the checkbox being flipped — the gate doing its job |

## 7. Adversarial review round

An independent reviewer (fresh context, frozen working tree; full report at
`%TEMP%/cuo-review-online-ui-console-removal.md`, with its matcher port and censuses kept under
`%TEMP%/cuo-review-work/`) returned **0 blocker / 0 major / 4 minor / 6 nit**. It reproduced every
recorded number from the frozen tree (build 0/0, pin 15/15, family 103/103, gates 208/208, red
6/9/15), ported the pin's matchers line for line outside the repository and ran a mutation battery
against them, and closed the cycle's missing full-suite gap itself (main 4069 + gates 208, exit 0).

Disposition, all in this commit:

- **M1 — two evidence records still named the deleted surface** (`ui/command-console-selfcheck.md`'s
  header and mechanism row 14, `ui/chat-selfcheck.md`'s closing note, which the MANIFEST marks
  current). Fixed: all three now say the page was deleted by this cycle and that the slash-opened
  overlay is the only command and chat surface.
- **M2 — the key census counted textual mentions rather than reads**, so a key hidden in a block
  comment or a log literal could stand in for a deleted read (exhibited). Fixed: the census matches
  the read shape `T("key")` and `Flatten` strips block comments as well as line comments; two new
  samples (block-comment key, log-literal key) pin the fix.
- **M3 — the tab-row and switch facts pinned two member bodies, not the file**: a seventh tab drawn by
  the content pass, or an inline console panel after the switch, passed every positive fact
  (exhibited). Fixed: the window must draw exactly six tabs anywhere in it and call the tab row
  exactly once, and a new fact requires the in-game command buffer to be read by the overlay alone;
  two new samples (seventh tab, inline panel) pin it.
- **M4 — no full-suite artifact existed before the review, and the family filter missed the
  `CommandConsole*` families** that consume the touched service. Fixed: the intermediate family run
  now includes `CommandConsole`/`ConsoleInput` (178 cases), the reviewer's own full run is recorded
  above, and the cycle's final run is the unfiltered full suite with build (§6).
- **N1** — the review brief over-stated the red as "seven facts"; these records were already exact
  (six contract failures, with `BothLanguagesCarryEverySurvivingKey` passing at HEAD). No change.
- **N2 — the format evidence did not support "exit 0, no file changed"** (a 113-byte workspace-load
  warning with no exit code). Fixed by re-running `dotnet format` with its exit code captured and the
  tree compared before and after (§6).
- **N3 — the new selfcheck had no MANIFEST row.** Fixed: the row is added. The pre-existing drift (198
  rows against 262 files) and the manifest intro's stale `docs/selfchecks/` path are recorded as
  `review/catalogue-and-manifest-census-drift.md` rather than fixed here.
- **N4 — the ticket's `## Evidence` kept the pre-change tense** ("on both surfaces"). Fixed: "at the
  time of the ruling".
- **N5 — "deleted rather than left as orphans" read as a policy the cycle does not apply
  family-wide** (81 unreferenced keys remain elsewhere). Fixed: the ticket's Limits name the 81-key
  census and point at the ticket that will decide the policy.
- **N6 — the window-state fact was a substring test in both directions** (a renamed buffer passed; a
  `ConsoleInputSession` reference failed spuriously). Fixed: the field match is word-bounded, pinned
  by a new tolerance sample.

The reviewer's own limits: it did not run `dotnet format` (it rewrites files) and did not reproduce
the red inside the repository (that would rewrite the frozen tree) — it reproduced the red at matcher
level with its own port instead. No rendering was observed, so acceptance rows 1-3 stay the user's
real-session rows, a limit the reviewer agreed with rather than treated as a gap.

## 8. Limits

- The suite proves the source shape and the key census; it cannot draw a frame. Acceptance rows 1-3 —
  the tab row as rendered, `/` still opening the overlay with completion, history and suggestions,
  and a command or chat line still behaving — are the user's real-session rows.
- The key census covers the `tab.` and `console.` prefixes, which is where this page's keys live, and
  counts the `T("…")` read shape. Another key family, another accessor or a dynamically built key is
  outside its shape by construction — and a key that stops being read fails loudly instead of passing
  silently, which is the direction that matters here.
- The shape facts read text: a hand-inlined panel that names neither a drawer type nor `ctx.Commands`
  and draws its own string literals is beyond any text pin, though it would have to rebuild the
  console the cycle just deleted and the key census would catch a new catalogue key for it.
- The tab row and the switch are pinned as exact bodies (whitespace-normalized, comments stripped): a
  formatting change to `DrawTabs` or the switch fails the pin until it is re-stated deliberately.
- `console.hint` and `console.overlay.controls` had no reader anywhere in the tree at HEAD, before
  this cycle's deletion; the two `console.*` keys the page family left behind are removed with it.
  81 further declared-and-unreferenced keys exist in other families and are deliberately NOT swept
  here — recorded as `review/catalogue-and-manifest-census-drift.md` so the policy is decided once.
  Nothing renders the overlay's controls line from the catalogue today, recorded rather than changed.
- The MANIFEST's pre-existing incompleteness (198 rows against 262 files) is untouched beyond adding
  this cycle's own row; see the same ticket.
