# The repository's history carries machine and personal identifiers

- Status: Todo — **opened 2026-10-12 by the user's directive**, after the acceptance-records ticket
  (`docs/backlog/todo/acceptance-records-carry-real-steam-ids.md`) raised the history question and the user
  ruled on it: the history is rewritten and force-pushed, and the durable half is a PRE-COMMIT CHECK rather
  than a gate. The originating ticket keeps the worktree sweep and the real Steam ids; this one owns the
  history, the rest of the identifier surface, and the process.
- Priority: High — `AGENTS.md` states the machine-identifier rule as a red line and it covers three places:
  file contents, commit messages and history. A worktree sweep leaves the third one carrying every value that
  ever entered, and this repository is public and written for strangers to deploy.
- Category: Security / repository hygiene
- Related: `docs/backlog/todo/acceptance-records-carry-real-steam-ids.md` (the Steam-id census and the
  worktree sweep), `AGENTS.md` (*Absolute-machine-path red line*, and the global rule that a machine
  fingerprint must never enter the repository or its history), `docs/en/contributing/review-and-delivery.md`
  (where the pre-commit check's rule statement goes), `docs/acceptance/AGENTS.md` (rule 7: placeholders only),
  `docs/standard/alignment.txt` (a page edited on the English side must re-record its pair hash),
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`
  (`NoAbsolutePaths_NoTrackedMachinePaths` — the gate this ticket explicitly does NOT extend)

## What the user decided (2026-10-12, and the reasoning, because it constrains the fix)

- **Rewrite the history and force-push.** The alternative — accepting that the owner's own ids are published —
  was offered and refused.
- **Do NOT make the scan a gate.** Offered as an option and refused, with the reason stated: this class of
  scan both misses (a real value inside a quoted log line, a value whose shape is not on the list) and
  false-positives (the synthetic fixtures `src/` and `tests/` use as data, which identify nobody and whose
  flagging would make the code unreadable). A gate that cannot equal its reach teaches false confidence and
  costs a maintenance burden that never converges.
- **Make it a pre-commit CHECK instead**, with the effort scaled to the change: a change touching many
  domains or anything near credentials, identity or machine facts gets an independent subagent scan of the
  diff; a small single-domain change is scanned in the working session. The check reports evidence and either
  passes the commit or sends it back.

## Required work

1. **Assemble the censuses into ONE list before touching anything.** Three sources, none of which is complete
   alone: the Steam-id census (the sibling ticket), the full-history scan of every other identifier category
   (machine absolute paths, host names and addresses, account and login names, personal data, credential-
   shaped literals, fingerprintable readings), and the commit messages — the third position, which the
   2026-10-06 incident is the precedent for. The scan must say, per hit: the value, whether it is in the
   history, the current tree, or a commit message, how many files and commits carry it, and whether it is a
   real value or a recorded synthetic fixture.
2. **Sweep the working tree first and commit that**, so the rewrite has less to carry: every real value in a
   tracked file becomes the placeholder vocabulary the records already use (`<owner>`, `<operator>`,
   `<treated>`, `<peer>`, `<game-dir>`, `<sandbox-root>`). A quoted log line keeps its shape and only the
   value changes.
3. **Rewrite the history**, following this machine's recorded procedure
   (`.agent-local/git-history.md` in the local area): a LOCAL backup branch first with the old SHAs recorded,
   the rewrite itself, then `git rebase --gpg-sign --no-ff <base>` re-signing every commit (`%G?` all `G`
   before the push), equivalence proven per commit against the backup, and the force-push done with
   `--force-with-lease=<ref>:<old-sha>`. The 2026-10-03 purge is the working precedent, including its trap:
   the rewrite checks the working tree out at the rewritten commit, so any file the rewrite removes must be
   restored from its backup blob byte for byte and kept out of the index.
4. **Write the pre-commit check into the process pages**, both language blocks in the same change: the rule
   in `docs/en/contributing/review-and-delivery.md` (with the Chinese block's page beside it, whose pair hash
   is then re-recorded in `docs/standard/alignment.txt`), naming what to look for, the delegation rule
   (many domains or an identity/credential surface → an independent subagent; a small change → the working
   session), and the honest limit ("this is a check, not a gate; it cannot enumerate every shape").
5. **Record the decision** where the next reader looks for it, so the question is not re-opened: the ruling,
   its date, and the two rejected options with their reasons.
6. **Verify** with the same scans that produced the censuses: zero hits outside the gitignored local files,
   in the current tree AND in the history AND in the commit messages, with the synthetic fixtures still
   present and still unflagged.

## Acceptance rows

| # | Row | Class |
|---|---|---|
| 1 | A repeat of the history-wide scans answers zero for every real value, over the current tree, the full commit contents and the commit messages | `machine` |
| 2 | The synthetic fixtures under `src/` and `tests/` are still present and were never flagged, and the code and its tests still read unchanged | `machine` |
| 3 | The remote's history no longer carries any of the values, and every rewritten commit is signed | `machine` |
| 4 | The process pages carry the check in both language blocks, and a reader who has never seen this ticket can tell what to scan for and when to delegate it | `record` |

## Non-goals

- Not a gate. The user refused one with the reason above; adding a scan-shaped gate "as well" would contradict
  the ruling rather than follow it.
- Not the lobby ids, item-instance ids or world ids the records cite: they are per-session identifiers, not
  identities.
- Not a change to what a record may say about a run — only about how it names machines and people.
