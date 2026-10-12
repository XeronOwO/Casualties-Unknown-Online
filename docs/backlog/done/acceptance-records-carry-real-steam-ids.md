# The acceptance records carry the machine's real Steam account ids

- Status: Done — the sweep this ticket owned landed in the 2026-10-12 purge cycle, and the history half it
  handed over landed with it (the purge ticket, `purge-machine-identifiers-from-history.md`); required work
  2 and acceptance row 2 are SUPERSEDED by the user's ruling of 2026-10-12 rather than met — the scan is a
  pre-commit check, not a gate (decision 261). The census below is the 2026-10-11 one; the purge ticket's
  census supersedes its figures.
- Priority: Medium — hygiene of a rule the repository states as binding, and a public repository is the place
  the rule exists for; it is not a runtime defect, and no acceptance row depends on it.
- Category: Process / documentation hygiene
- Related: `docs/acceptance/AGENTS.md` (rule 7: "No machine path, no personal data in git. Records, index rows
  and commits carry placeholders only"), `AGENTS.md` (*Document Scope & Classification*: machine- and
  person-specific facts live in the gitignored local files), `docs/AGENTS.md` §5,
  `docs/en/contributing/review-and-delivery.md` (the pre-commit check that replaced the proposed gate),
  `docs/decisions/active.md` (decision 261), `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`
  (`NoAbsolutePaths_NoTrackedMachinePaths`, and `NoLocalEnvironmentNames_InProductionCode`)
- Source: the three accounts this machine's acceptance runs use appear by their raw 17-digit SteamIDs in
  tracked files, in prose and inside quoted log lines.

## What landed (2026-10-12)

Every occurrence in the tracked tree became the placeholder the records already use — `<host>` for the
account the physical machine runs, `<guest>` for the primary sandbox's, `<third-client>` for the alternate
one, chosen by reading each line's own sentence rather than by a blind replace — and the same pass took the
rest of the machine's values out with them (sandbox instance names, the harness's ports, the proxy's name
and address, a run's process ids): 114 tracked files, one replaced span per occurrence — most files carry a
single changed line, the busiest nine. A `git grep -E '7656119[0-9]{10}'` over the tracked tree, and the
rule table's own check pass, both answer with nothing but the synthetic fixtures — the one id a search still
finds under `docs/` is the synthetic number this ticket's own prose cites, in the paragraph that draws the
line between a real account and a fixture — while the fixtures under `src/` and `tests/` were never touched.
Two hand-fixes replaced what a value-level replace could not:
the test fixture that had mirrored this machine's ports now seeds its own, and the gate's matchers became
shapes (a loopback endpoint carrying a port; a private-network address) instead of this machine's numbers.

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
   files. — **Landed** 2026-10-12: 114 tracked files swept (this ticket's 37 plus every other file carrying
   one of the machine's values), and both `git grep` and the rule table's own check pass answer zero.
2. **The gate** (the durable half): a tracked-file scan in the shape
   `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` already uses. The ids themselves must NOT
   appear in the gate, so the check is "a 17-digit `7656119…` literal in a tracked documentation file is a
   violation unless it is one of the recorded synthetic fixtures" — an allowlist of the test ids, held in the
   test. — **SUPERSEDED 2026-10-12 by the user's ruling**: no scan-shaped gate for this class, a pre-commit
   check instead (decision 261), because such a scan cannot equal its reach while the synthetic fixtures
   would red it.
3. **The history half — ANSWERED by the user on 2026-10-12, and now owned elsewhere**: rewrite the history and
   force-push; the worktree sweep stays here, while the rewrite, the rest of the identifier surface (machine
   paths in file contents AND in commit messages) and the pre-commit check live in the purge ticket
   (`purge-machine-identifiers-from-history.md`), whose census supersedes this ticket's
   earlier 37-file figure. The same ruling refused a gate for the scan and made it a pre-commit check instead
   (a full subagent scan for a change that crosses domains, the working session for a small one).
4. **The discipline that let it in**: the acceptance record template and `workflow.md` §7 should say
   "placeholders only" where they already say "no machine values", and a batch's own pre-commit scan should
   check the ids it knows (its own run's accounts), which is how this was found — that last half is now the
   pre-commit check the purge ticket wrote into the process pages.

## Acceptance rows

| # | Row | Class | Verdict |
|---|---|---|---|
| 1 | `git grep` for the three real ids over the tracked tree answers zero, and the gitignored local files still carry them where a run needs them | `machine` | **pass** — a `git grep -E '7656119[0-9]{10}'` over the tracked tree names only the synthetic fixtures under `src/` and `tests/` and nothing under `docs/`; `docs/acceptance/AGENTS.local.md` still holds the values a run needs |
| 2 | The new gate fails on a sample record carrying a real-shaped id and passes on the synthetic fixtures | `machine` | **superseded** by decision 261 — the scan is a pre-commit check, so there is no gate to judge; the check's statement is in `docs/en/contributing/review-and-delivery.md` |
| 3 | Whatever the user decides about history is recorded as a decision with its scope (rewritten, or accepted as published) | `machine`/record | **pass** — decision 261 records the ruling (rewrite and force-push; commit metadata out of scope), and the rewrite's evidence is in the purge ticket |

## Non-goals

- Not the absolute-path red line: that one has a gate and is green (`RepositoryGateTests`).
- Not the lobby ids, item instance ids or world ids the records cite: they are per-session identifiers, not
  account identities.
- Not a change to what a record may say about a RUN — only about how it names the accounts.
