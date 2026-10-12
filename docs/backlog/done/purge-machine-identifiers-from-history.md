# The repository's history carries machine and personal identifiers

- Status: Done — **opened 2026-10-12 by the user's directive**, after the acceptance-records ticket
  (`docs/backlog/done/acceptance-records-carry-real-steam-ids.md`) raised the history question and the user
  ruled on it: the history is rewritten and force-pushed, and the durable half is a PRE-COMMIT CHECK rather
  than a gate. The originating ticket kept the worktree sweep and the real Steam ids; this one owned the
  history, the rest of the identifier surface, and the process — all landed in the same day's cycle (see
  *What landed*), with one residual the user asked for: the local backup branch.
  Two premises of this ticket were corrected by its own census, and the corrections are the reason it ran
  long: the commit MESSAGES are a real position on `master` (not only off it), and the replacement
  vocabulary is per CLASS (`<host>`/`<guest>`/`<third-client>`), not the four names the ticket guessed.
- Priority: High — `AGENTS.md` states the machine-identifier rule as a red line and it covers three places:
  file contents, commit messages and history. A worktree sweep leaves the third one carrying every value that
  ever entered, and this repository is public and written for strangers to deploy.
- Category: Security / repository hygiene
- Related: `docs/backlog/done/acceptance-records-carry-real-steam-ids.md` (the Steam-id census and the
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

## The census (2026-10-12, on a frozen tree — this is the authoritative one)

The 2026-10-11 report was produced while another session was editing the same working tree, and it read
the reachable commits only. This census ran at `406fa927` on a tree with no modifications, and read
**every object in the database** (`git cat-file --batch-all-objects`: 15,804 blobs, 245.6 MiB), which
also closes that report's "`git fsck` was not run" gap. Reproduce it with
`.agent-local/rewrite-20261012/scan-blobs.mjs` (values) and `discover.mjs` (classes), both gitignored.

| What | Where | Size |
|---|---|---|
| The three real Steam account ids | tree + history | 37 tracked files; the largest share of the 572 dirty blobs |
| This machine's game directory and both Sandboxie redirection roots, written with either slash | history only | HEAD carries none; the commit-message hit below does |
| Sandboxie instance names | tree + history | 71 tracked files |
| The acceptance harness's local ports | tree + history | 31 tracked files |
| The local proxy's product name and address | tree + history | 3 tracked files when the report ran, 1 after `406fa927` |
| A run's process ids | tree + history | 4 tracked files |
| Commits carrying a value in their CONTENT, on `master` | history | **1468 of 1473** (the five clean ones are the repository's oldest); 195 distinct paths ever carried one |
| Commits carrying a value in their MESSAGE, on `master` | history | **2** — `fa9f4035` (the proxy's product name) and `495ddb55` (a sandbox path written with forward slashes) |
| A commit message carrying a machine path, off `master` | history | `15bf9e41`, held up by two local backup branches |
| Dirty blobs | database | **572**, of which **4 are unreachable from every ref** (dangling objects earlier rewrites left); the reflog expiry plus `git gc` is what removes those four, and deleting the two backup refs that hold the off-`master` message commit is what removes that one — the backup branch this cycle KEEPS, by the user's instruction, deliberately keeps the rest |
| A user-profile path, or a build-log path under a user profile | database | two dangling blobs pasted from a build log; nothing on the tip, and no such path is reachable from any ref |
| Credentials, private keys, `.env` files, emails in contents, the machine's Windows profile name | all three positions | **none** — the profile name `23980` appears in no blob, commit or message. The author's own handle is the one identity that stays: it is the git author name, it is on the `LICENSE` line, and the 2026-10-12 ruling leaves the author identity out of scope |
| The class count the discovery pass sweeps | database | twenty classes, so a value the replacement table does not list still surfaces for judgement |

Judged and deliberately NOT swept, each with its reason: the synthetic Steam ids under `src/` and
`tests/` (fixtures — they identify nobody, and a scan that flags them makes the code unreadable); a run's
log volume (`1.3 GB`, and the selfchecks' megabyte censuses) because it is the measurement a defect claim
rests on rather than a fact about the machine; `2 GiB`, a decoder boundary the tests exercise; the loopback
address on its own; `Sandboxie` / `SbieSvc` / `/box:<box>`, a public third-party tool and its own
placeholder; the path-traversal fixtures the save-archive tests use as hostile payloads; and git's commit
metadata — the author name and
address every commit records — by the user's ruling of 2026-10-12 ("git 提交信息不算敏感信息，这个不管"),
recorded as decision 261.

Two further corrections to the report's picture: the remote carries **only** `master` (verified with
`git ls-remote`, 2026-10-12) — the two backup branches it found under `refs/remotes/origin/` were already
gone from the host, so those stale tracking refs are pruned locally rather than deleted remotely; and
**the commit messages are a real position on `master`**, not just off it, which is why the rewrite carries
a message filter as well as a content filter.

What this census does NOT reach, stated so nobody reads the zeroes as wider than they are: the class list
is a list, so a value of a class nobody thought of is only found if the discovery pass happens to match it
(it is the reason that pass sweeps twenty classes rather than the replacement table's own patterns); a
binary blob is skipped by `git grep`'s text filter; and the replacement table maps the machine's Steam
library root and its game directory to the same `<game-dir>` placeholder, so a sentence that contrasts the
two would lose the distinction — sampling found no such sentence, and the alternative (a second
placeholder for the library root) was not worth a vocabulary entry nothing else uses.

## Required work

1. **Assemble the censuses into ONE list before touching anything.** Three sources, none of which is complete
   alone: the Steam-id census (the sibling ticket), the full-history scan of every other identifier category
   (machine absolute paths, host names and addresses, account and login names, personal data, credential-
   shaped literals, fingerprintable readings), and the commit messages — the third position, which the
   2026-10-06 incident is the precedent for. The scan must say, per hit: the value, whether it is in the
   history, the current tree, or a commit message, how many files and commits carry it, and whether it is a
   real value or a recorded synthetic fixture. — **Done** (the census above; the sibling ticket's 37-file
   figure is superseded, and the rule table behind it is `.agent-local/rewrite-20261012/lib/rules.mjs`).
2. **Sweep the working tree first and commit that**, so the rewrite has less to carry: every real value in a
   tracked file becomes the placeholder vocabulary the records already use (`<owner>`, `<operator>`,
   `<treated>`, `<peer>`, `<game-dir>`, `<sandbox-root>`). A quoted log line keeps its shape and only the
   value changes. — **Done**, with the vocabulary chosen per CLASS rather than per ticket: an account that is
   a run ROLE is `<host>` / `<guest>` / `<third-client>` (all three already in the records, and the choice
   this ticket did not anticipate), the boxes are `<sandbox-guest>` / `<sandbox-alt>`, the evaluator ports are
   `<port-host>` / `<port-guest>` / `<port-alt>`, and the paths keep `<game-dir>` / `<sandbox-root>` /
   `<sandbox-alt-root>`. One replaced span per occurrence, in 114 tracked files; a quoted log line keeps its
   shape throughout, and the two occurrences a value-level replace could not fix were hand-edited (the test
   fixture that had mirrored the machine's ports, and the gate whose matchers spelled the values out).
3. **Rewrite the history**, following this machine's recorded procedure
   (`.agent-local/git-history.md` in the local area): a LOCAL backup branch first with the old SHAs recorded,
   the rewrite itself, then `git rebase --gpg-sign --no-ff <base>` re-signing every commit (`%G?` all `G`
   before the push), equivalence proven per commit against the backup, and the force-push done with
   `--force-with-lease=<ref>:<old-sha>`. The 2026-10-03 purge is the working precedent, including its trap:
   the rewrite checks the working tree out at the rewritten commit, so any file the rewrite removes must be
   restored from its backup blob byte for byte and kept out of the index. — **Done, with one deliberate
   deviation on the signing step** (see *What landed*): re-signing was folded into the rewrite pass
   (`--commit-filter 'git commit-tree -S "$@"'`) instead of a following `git rebase --gpg-sign`, because
   `git rebase` re-stamps every committer date while `filter-branch` passes the original through; `%G?` all
   `G` is met either way, and the dates are preserved as a bonus the rebase route cannot give.
4. **Write the pre-commit check into the process pages**, both language blocks in the same change: the rule
   in `docs/en/contributing/review-and-delivery.md` (with the Chinese block's page beside it, whose pair hash
   is then re-recorded in `docs/standard/alignment.txt`), naming what to look for, the delegation rule
   (many domains or an identity/credential surface → an independent subagent; a small change → the working
   session), and the honest limit ("this is a check, not a gate; it cannot enumerate every shape"). — **Done**
   (the new section in both blocks, hashes re-recorded).
5. **Record the decision** where the next reader looks for it, so the question is not re-opened: the ruling,
   its date, and the two rejected options with their reasons. — **Done** (decision 261).
6. **Verify** with the same scans that produced the censuses: zero hits outside the gitignored local files,
   in the current tree AND in the history AND in the commit messages, with the synthetic fixtures still
   present and still unflagged. — **Done for everything that is published**; the local backup branch the user
   asked to keep is the one named exception (see *What landed*).

## What landed (2026-10-12)

The cycle's commits: `0a4359c4` (the sweep, the gate's shapes, the process pages, decision 261) was rewritten
to `402db461`, and the record you are reading landed on top of it as `d909ae33` — the pushed `master`.

**The rewrite, and what proves it.** `master` was rewritten with `git filter-branch` over `9565fd53..master`
(1,473 commits; the root is preserved, see below) — the pass applied an `--index-filter` swapping each dirty
blob for its pre-computed twin (572-entry map), a
`--msg-filter` running the same rule table over every commit message, and `--commit-filter` re-signing each
commit. Evidence, all reproducible from `.agent-local/rewrite-20261012/`:

- **Every commit's identity is preserved**: 1,474/1,474 checked by `equivalence-check.mjs` — author,
  committer (names, addresses and DATES), parents mapped through the old→new mapping, and the message equal
  to the rule table's output — **zero findings**.
- **Every commit's content is exactly the old content with the rules applied**: the same run compares the
  whole tree, path and mode included, and requires every blob outside the map to be byte-identical —
  again zero findings.
- **The tip did not move**: `402db461^{tree}` is `bad3a1a2…`, byte-identical to the tree of `0a4359c4` before
  the rewrite. That single hash is what rules out an empty or truncated index filter.
- **The history is signed**: `git log --format='%G?' master` is `G` × 1,474 and `U` × 1 — the exception is
  the root commit, preserved deliberately (see below): it carries GitHub's own web-flow signature.
  The 140 commits that were unsigned before are signed now.
- **The values are gone from everything published**: `verify-scan.mjs --rev master`, and the same scan over
  the fetched `origin/master`, each answer 0 content commits, 0 paths and 0 message commits (they answered
  1,468 / 195 / 2 before). The two rewritten messages are
  the ones the census predicted, and only those two: `189f266c` ("sandbox moved to
  `<sandbox-root>`+`<sandbox-alt>`") and `cbeebf23` (the local proxy's product name).
- **The off-`master` message leak is deleted, not rewritten**: `15bf9e41` was reachable only from the stale
  refs the user approved deleting; after `git reflog expire --expire=now --all` + `git gc --prune=now` the
  object is gone (`git cat-file -t` answers "could not get object info") and `git fsck` is silent.
- **The dangling set is gone too**: the 4 unreachable dirty blobs — including two pasted build-log paths
  under a user profile — were pruned by the same `gc`; the census now reports **0 dangling dirty blobs**.
- **The remote**: `origin` carries exactly one branch, `master` = `d909ae33`; the force-push used
  `--force-with-lease=refs/heads/master:6a389f5a…`, and the scans were re-run against the ref fetched back
  from it rather than against the local one.

**The push was rejected once, and the fix is worth keeping.** The first pass re-signed every commit,
including the root, and GitHub's repository rule ("Commits must have verified signatures") refused it: the
root commit's COMMITTER is `GitHub <noreply@github.com>` — the web UI created it — so a signature made with
this machine's key cannot verify against that identity. The fix is not a bypass: the root commit is one of
the five clean commits of the census, so it is **preserved verbatim** (GitHub's own signature, exactly as the
remote had already verified it) and only `9565fd53..master` was rewritten — 1,473 commits. GitHub's
web-flow public key was then imported locally, where the root reports `U` rather than `G` because the key
arrives without local trust.

**The residual, and it is deliberate.** The user asked to keep ONE local backup branch, so
`backup/pre-identifier-purge-20261012` (`0a4359c4`) still holds the pre-purge history on this machine — the
only copy of it that exists anywhere. It is not pushed, and it is why the whole-database census still finds
557 dirty blobs after the rewrite (they are that branch's history and nothing else). Deleting it, expiring
the reflog and running `gc --prune=now` is what makes even this machine clean, and that is the user's call
once they are satisfied with what the remote shows.

**Two mistakes this cycle made and fixed, recorded because they cost the most time.** The first loose-object
writer omitted git's `blob <size>\0` header, so 572 objects git could not unpack were written. Worse, the
cleanup then deleted object files by path on the assumption that a file at a map value's name could only be
one this cycle wrote — false, because a rewritten blob is often byte-identical to a blob the repository
already had, and **109 reachable objects were lost** (`git rev-list --objects --all` answered
`fatal: missing blob`). They were restored by regenerating each from its map key, requiring the result to
hash back to the expected object name: 572 pairs, zero mismatches, `git fsck` clean again. Both lessons are
in `.agent-local/git-history.md`.

## Acceptance rows

| # | Row | Class | Verdict |
|---|---|---|---|
| 1 | A repeat of the history-wide scans answers zero for every real value, over the current tree, the full commit contents and the commit messages | `machine` | **pass for everything published, with the kept backup branch as the named exception**: `sweep-tree.mjs --check` = 0 of 3,775 files; `verify-scan.mjs --rev master` = 0 content commits, 0 paths, 0 message commits; the whole-database census still finds 557 dirty blobs and every one of them is `backup/pre-identifier-purge-20261012`'s history, which the user instructed this cycle to keep as the safety net |
| 2 | The synthetic fixtures under `src/` and `tests/` are still present and were never flagged, and the code and its tests still read unchanged | `machine` | **pass** — the fixture ids are untouched (`git diff 0a4359c4 055f4f00 -- src tests` shows no test-fixture change beyond the hand-rewritten preflight fixture, which uses its own ports now), the rule table has no pattern that can match them, and the suite is green at 5,068 behaviour cases and 625 gates |
| 3 | The remote's history no longer carries any of the values, and every rewritten commit is signed | `machine` | **pass** — `git log --format='%G?' master` is `G` × 1,474, the force-push used `--force-with-lease=<ref>:6a389f5a…`, and the scans were re-run against the ref fetched back from `origin` |
| 4 | The process pages carry the check in both language blocks, and a reader who has never seen this ticket can tell what to scan for and when to delegate it | `record` | **pass** — the new section in `docs/en|zh/contributing/review-and-delivery.md` names what to look for, the delegation rule, the two shapes the gates do reach, and the limit; `docs/standard/alignment.txt` records the new pair hashes |

## Non-goals

- Not a gate. The user refused one with the reason above; adding a scan-shaped gate "as well" would contradict
  the ruling rather than follow it.
- Not the lobby ids, item-instance ids or world ids the records cite: they are per-session identifiers, not
  identities.
- Not a change to what a record may say about a run — only about how it names machines and people.
