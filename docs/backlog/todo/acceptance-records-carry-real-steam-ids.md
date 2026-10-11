# The acceptance records carry the machine's real Steam account ids

- Status: Todo — **found 2026-10-11 by batch `20261011-k`'s pre-commit fingerprint scan**, which ran because
  the batch's own new files had to be checked before they entered git. The run did not sweep: a worktree
  sweep cannot finish this, and the history half is the user's decision (see *Required work*).
- Priority: Medium — hygiene of a rule the repository states as binding, and a public repository is the place
  the rule exists for; it is not a runtime defect, and no acceptance row depends on it.
- Category: Process / documentation hygiene
- Related: `docs/acceptance/AGENTS.md` (rule 7: "No machine path, no personal data in git. Records, index rows
  and commits carry placeholders only"), `AGENTS.md` (*Document Scope & Classification*: machine- and
  person-specific facts live in the gitignored local files), `docs/AGENTS.md` §5,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs` (the absolute-path gate this
  ticket's fix should be the sibling of)
- Source: the three accounts this machine's acceptance runs use appear by their raw 17-digit SteamIDs in
  tracked files, in prose and inside quoted log lines.

## The census (2026-10-11, `git grep` over the tracked tree)

| What | Count |
|---|---|
| The three real account ids, occurrences | **117** |
| Files carrying at least one | **37** — 32 under `docs/evidence/` (records and self-checks), 5 under `docs/backlog/` (tickets) |
| First commit that introduced one | `97173282`, **2026-09-27** (the session batches' first records) |

Deliberately NOT in scope: the synthetic ids the code and its tests use as data
(`76561198000000000`, `76561198000001001` and friends under `src/` and `tests/`). They identify nobody, they
are fixtures, and a scan that flags them would make the code unreadable. The line is "is this account real",
not "does this string look like a SteamID".

## Why it matters

- The repository is public and made for strangers to deploy; its own rules put machine and person specific
  facts in the gitignored local files (`AGENTS.local.md`, `docs/acceptance/AGENTS.local.md`,
  `.agent-local/`), and the acceptance area's rule 7 repeats it for records, index rows and commits.
- The records' vocabulary is the target, not the current state: `<owner>`, `<operator>`, `<treated>` and
  `<peer>` already exist in this repository's records, but only a couple of the 37 files use them beside a raw
  id — the sweep is per-file judgement (which account is which in THIS record's sentences), not a global
  find-and-replace, and its cost should not be read as trivial.
- A reader cannot tell an id from a fingerprint: the same numbers are the accounts' public Steam identities
  and, in a save path, the key a character file is written under.

## Required work

1. **The sweep**: replace every occurrence in the 37 tracked files with the placeholder vocabulary the
   records already use, keeping each quoted log line readable (a quoted line keeps its shape; only the id
   becomes a placeholder). Verify with a `git grep` for the three ids: zero hits outside the gitignored local
   files.
2. **The gate** (the durable half): a tracked-file scan in the shape
   `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` already uses. The ids themselves must NOT
   appear in the gate, so the check is "a 17-digit `7656119…` literal in a tracked documentation file is a
   violation unless it is one of the recorded synthetic fixtures" — an allowlist of the test ids, held in the
   test.
3. **The history half, which is the user's call**: the ids entered history on 2026-09-27 and live in every
   later record. A worktree sweep does not remove them; only a history rewrite plus a force-push does, which
   is what this repository did once before (2026-10-06). Ask the user whether the records' history is
   rewritten (with a local backup branch first, and the ids' commits listed) or whether the owner's own
   account ids are accepted as published. Nothing else in this ticket depends on that answer.
4. **The discipline that let it in**: the acceptance record template and `workflow.md` §7 should say
   "placeholders only" where they already say "no machine values", and a batch's own pre-commit scan should
   check the ids it knows (its own run's accounts), which is how this was found.

## Acceptance rows

| # | Row | Class |
|---|---|---|
| 1 | `git grep` for the three real ids over the tracked tree answers zero, and the gitignored local files still carry them where a run needs them | `machine` |
| 2 | The new gate fails on a sample record carrying a real-shaped id and passes on the synthetic fixtures | `machine` |
| 3 | Whatever the user decides about history is recorded as a decision with its scope (rewritten, or accepted as published) | `machine`/record |

## Non-goals

- Not the absolute-path red line: that one has a gate and is green (`RepositoryGateTests`).
- Not the lobby ids, item instance ids or world ids the records cite: they are per-session identifiers, not
  account identities.
- Not a change to what a record may say about a RUN — only about how it names the accounts.
