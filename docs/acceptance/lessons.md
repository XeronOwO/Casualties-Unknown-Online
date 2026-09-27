# Acceptance lessons

What the runs keep teaching. Every acceptance run folds its reusable lessons back here before it
closes ([AGENTS.md](AGENTS.md) rule 9); a lesson that stays in a transcript is lost, and the next
batch pays for it again. Machine values and local gotchas belong in `AGENTS.local.md`, not here; a
rule that binds the whole repository graduates to `docs/AGENTS.md` or `AGENTS.md`.

## How to add an entry

One entry per lesson, newest last, in this shape:

```text
## <date> — <what happened, as a headline>

- Symptom: <what the run saw>
- Cause: <the mechanism, not the story>
- Change: <the file and rule that now carry it>
```

An entry is worth adding when it would change what the next run does: a preflight check that lied, a
scenario setup that turned out to need something extra, an evidence kind that was not convincing, a
dependency the table did not name, a step that cost more than it returned.

## 2026-09-27 — The preflight matched nothing on its first run (PowerShell 5.1 code pages)

- Symptom: `preflight.ps1` reported `no 验收环境 section resolved` and then `FACT-MISSING` for every
  key, while the facts file plainly carried the section.
- Cause: Windows PowerShell 5.1 reads a UTF-8 file without a BOM — the script itself, and the facts
  file it read — through the system code page, so the Chinese heading in the script and the one in
  the file were mangled into two different strings that could never match.
- Change: the script is ASCII-only and matches an ASCII section marker
  (`## 验收环境 / acceptance environment`), and reads the facts with an explicit `-Encoding UTF8`.
  A committed script must never depend on how the host guesses its own encoding.

## 2026-09-27 — Auto-loaded instruction files are a budget, and it was nearly spent

- Symptom: adding the area's two instruction files (measured on the working tree, 2026-09-27:
  `docs/acceptance/AGENTS.md` 4,799 bytes + `docs/acceptance/AGENTS.local.md` 2,670 bytes = 7,469) put
  the sum of every instruction file 906 bytes over the 65,536-byte budget, while the always-present root
  pair already used 90% of it.
- Cause: nobody had measured the budget before adding two auto-loaded files, and the gate counted every
  nested file into the same budget — which is not what the loader charges. It charges the always-present
  pair and drops the user-level instruction file when they leave no room (the session reported exactly
  that).
- Change: `AgentInstructionBudgetGateTests` now measures what the loader charges — the always-loaded root
  pair against the budget (58,973 bytes that day: `AGENTS.md` 18,746 + `AGENTS.local.md` 40,227, leaving
  6,563), and every other instruction file, tracked or area-local, against its own 5,120-byte ceiling.
  That headroom is less than the 19,456-byte user-level file, so the loader still drops it; restoring it
  needs 12,893 bytes of cuts from the root pair. Measure a file's bytes before adding one, and prefer a
  page over a router.

## 2026-09-27 — An instruction file can be at its ceiling before you touch it

- Symptom: naming the new area in `docs/AGENTS.md` §1 pushed the file to 5,447 bytes and the nested
  ceiling gate refused the build; `docs/AGENTS.md` now sits at 5,1xx of its 5,120-byte allowance.
- Cause: the router had grown to within ~90 bytes of its own ceiling, so any added sentence breaks it —
  and the fix is not to raise the ceiling but to say the same thing in fewer words.
- Change: the fact was folded into the existing bullets and redundancy trimmed in §1/§2/§4/§7. Measure a
  file's byte size before editing an instruction file, and prefer moving detail into a page over growing
  the router.

## 2026-09-27 — A preflight that only reports is not yet a run

- Symptom: the first green-ish preflight reported `artifacts` missing because the artifact directory
  did not exist; nothing was wrong with the machine.
- Cause: a writable evidence directory is a dependency like any other, and creating it is setup the
  run owns — not a question for the user.
- Change: a missing artifact directory is created by the run before it asks anything; the preflight
  keeps reporting it, because "not created yet" and "not writable" look the same from outside.

## 2026-09-27 — A machine fact can be green and still be the wrong game

- Symptom: `steam://rungameid/<game-app-id>` launched an unrelated game (Forts) on the acceptance
  machine, while the `steam` preflight row read `present` and even echoed the app id.
- Cause: the local facts file carried the wrong `game-app-id`. The preflight proves only that the key
  is set; it never proves that the id maps to the `game-dir` install, and the launch step consumes the
  fact directly, so the wrong value reached a real launch. The correct id is in the install's own
  `steam_appid.txt` and in the app manifest's name.
- Change: the fact is corrected, and a run must cross-check the id against the install before
  launching. The preflight now does that: the `steam` row compares `game-app-id` with the install's
  own `steam_appid.txt` and with the library app manifest's `installdir`, requires every source that
  exists to confirm it, and blocks a launch when one contradicts or none can confirm.

## 2026-09-27 — An interrupted test run leaves a testhost that reddens the next one

- Symptom: a later full-suite run failed to build its test project (MSB3026/MSB3027, "file is being
  used by another process", `testhost.net48`), while the gate project passed 288/288 in the same run.
- Cause: an earlier `dotnet test` invocation that never completed left `testhost.net48` holding
  `xunit.abstractions.dll` in the test project's `bin` directory.
- Change: check for `testhost*` processes started by this run and stop them before re-running the
  suite. A full-suite red that is a file lock is an environment finding, not a product finding.

## 2026-09-27 — In-process control is the driver this machine allows

- Symptom: the session batch looked blocked on the staged `input` capability; asking the user settled
  it: the agent may drive the game from inside its process, but must never take over the physical
  mouse and keyboard.
- Cause: the `input` row describes scripted keyboard/mouse driving — neither built nor permitted here.
  What the run actually needs is the in-process evaluator (`hotrepl-*`), which is present.
- Change: session setups are driven through the evaluator (create/join were both proven this way);
  the `input` row's capability and degradation now name in-process control instead of OS input, and its
  committed driver helper under `tools/acceptance/` stays staged until the harness lands.

## 2026-09-27 — A title-screen click is not "start the game"

- Symptom: the probe created the lobby (`Session role: Host`) and the guest joined
  (`role=Guest`), but invoking the title screen's SleepingBag `AdaptiveButton.Clicked()` shut the
  host client down cleanly instead of starting a run.
- Cause: each title-screen object carries its own action, and nothing in the object's name says which
  one starts a run; calling a handler outside a real pointer event exercised whatever that object
  does. "It ran" is not "it did what the scenario needed".
- Change: session actions go through the Online UI's own entry points; before any in-process click of
  a scene object is used as a setup step, its handler's target must be identified from evidence.

## 2026-09-27 — A delegated triage needs a machine-checkable contract

- Symptom: three read-only triage subagents classified the 142 `review/` tickets; two reports listed
  slugs outside their own input slice and their headline counts disagreed with their own lists (one
  headlined "24 offline" while listing 44 slugs).
- Cause: the prompts fixed the rubric but not a verifiable contract, so a report could be internally
  inconsistent and still look finished.
- Change: a triage delegation must echo its exact input list, emit exactly one class line per slug,
  derive every count from those lines, and the orchestrator re-derives the counts from the file
  before any batch is built on it.

## 2026-09-27 — A fact value carries no prose

- Symptom: the corrected `game-app-id` fact carried its verification note inside the value, so the new
  cross-check reported the id as contradicting the very install it had just been verified against.
- Cause: everything after the colon is the value — the note was appended to it, and the launch line
  would have consumed the same string.
- Change: `AGENTS.local.md` carries the bare value with its note on a line of its own, and
  `dependencies.md`'s local-facts contract says that a value is taken literally.

## 2026-09-27 — Moving tickets must move their references

- Symptom: the 30-ticket move went red on three gate checks at once — 45 prose references
  (`review/<slug>.md` across 22 documents), five sibling relative links, and one test anchor in a new
  record that a substring check had passed.
- Cause: the folder is the status, but references are hand-written prose; the move script updated the
  index and the tickets only. The cross-reference gate exists for exactly this rot, and a substring
  match is not the anchor gate's member check.
- Change: a batch that moves tickets fixes the prose references, the sibling links and the anchors in
  the same change — including the ones inside the moved tickets, whose exempt record folders the gates
  do not scan — and validates anchors against the gate's own member set.

## 2026-09-27 — An offline batch is still a run

- Symptom: the offline batch needed the whole `workflow.md` §4 chain minus the clients — preflight,
  build, both suites, format, and the deploy + hash verification; a ticket whose acceptance names a
  deploy row was only decidable because that deploy was part of this run.
- Cause: "offline" describes the rows, not the run, and an earlier suite run is never this run's
  evidence.
- Change: batch `20260927-b` ran the chain itself and its records cite only its outputs; a candidate
  whose row needs a measurement or a comparison the run does not produce is dropped and stays in
  `review/`, with the reason in the batch scope page.

## 2026-09-27 — A row that names a past event is judged by its durable outcomes

- Symptom: a ticket's acceptance carried "an independent adversarial review in a fresh context
  covering ...", an event no later run can re-create.
- Cause: some rows are process records, not behaviours; reading them as "re-run the event" makes the
  ticket permanently un-acceptable, and reading them as "trust the ticket" is no evidence at all.
- Change: such a row is judged by the durable outcomes the event produced (its findings pinned by
  regression cases that pass in this run), the record's Limits says the event itself was not re-run,
  and no record claims the event happened in the batch.

## 2026-09-27 — A header bullet can span lines; an insertion must not split it

- Symptom: the `- Acceptance record:` bullet landed inside a multi-line `- Related:`/`- Source:`
  bullet in 11 of the 30 moved tickets; five had their original bullet truncated and its continuation
  lines re-parented onto the new bullet, and no gate could see it (`backlog/done/` is exempt).
- Cause: the move script anchored on the first line matching `^- Related:` and inserted at
  `anchor + 1` without checking whether the bullet continues on the following lines.
- Change: insert after the last continuation line of the header block (or before the blank line that
  ends it) and verify the insertion point on every ticket the script touches — rebuilding the ticket
  from its `review/` revision plus the two intended lines is the cheap way to make that exact.

## 2026-09-27 — A rejected ticket's status field still repeats its folder

- Symptom: `- Status: Rejected` in `todo/` turned `EveryTicketStatusFieldAgreesWithItsFolder` red —
  the field must start with the folder's label (`Todo`), so the first rejection this workflow
  executed could not be committed in the shape the pages prescribed.
- Cause: the acceptance pages wrote the verdict as if it were the field; `BacklogIntegrityGateTests`
  derives the expected prefix from the folder's canonical map ("the folder is the status; the field
  repeats it for a reader who opens the file").
- Change: a rejected ticket carries `- Status: Todo — Rejected (…)`, and the four pages that
  prescribed the bare form are corrected.
