# Acceptance record — Recipe `// args:` gate and CRLF working trees

- Ticket: `recipe-args-crlf-gate` — verdict: moved to `done/`
- Batch: `20261002-a` — offline batch; scope and limits: `docs/evidence/acceptance/20261002-a-scope.md`
- Commit: `9de1c888` (the cycle's implementation commit; this record lands with the cycle's
  documentation commit)
- Deployed artifact: none — the change is test-only, no client was started and the install was not
  touched
- Run: 2026-10-02 — the focus gate on the LF tree and on byte-verified CRLF working copies, the
  reverted-matcher run on the delivered content, the normative gate project, the full suite with build,
  and `dotnet format` exit 0
- Independent review: `.acceptance/20261002-a/review-report.md` — PASS (0 blocker, 0 major; 4 minor and
  4 nit, each answered in `.acceptance/20261002-a/review-response.md`)
- Dependencies: `dotnet` (build, both suites), `git` (attributes, history), repository inspection; no
  game client
- Artifacts: the `20261002-a` directory under `acceptance-artifacts-dir` — `red-focus-crlf.log`,
  `red-samples-final.log`, `green-focus-lf-final.log`, `green-focus-crlf-final.log`, `format-2.log`,
  `gates-1.log`, `full-1.log`, `gates-2.log`, `recipes-eol-before.txt`, `recipes-eol-crlf.txt`,
  `recipes-lf-backup/`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The gate reads LF and CRLF checkouts identically and stays green on both | machine | pass | `green-focus-lf-final.log` (4/4, exit 0) and `green-focus-crlf-final.log` (4/4, exit 0); the second ran over all 36 working copies converted to the CRLF the attributes declare — byte-verified (no lone CR before, pure CRLF after, `git hash-object --path` equal to the committed blob) and restored hash-equal afterwards |
| 2 | The CRLF samples discriminate — the pre-fix matcher fails the positives — and the negative still reports an invalid kind | machine | pass | `red-focus-crlf.log` (the pre-fix matcher over CRLF working copies: `the declared argument 'dmg=n<CR>'`) and `red-samples-final.log` (the delivered content with the matcher reverted: fails at the CRLF positive with `b=n\r`); the fixed matcher passes all four samples |
| 3 | No false positives: all 36 recipes pass under CRLF | machine | pass | the directory scan inside `green-focus-crlf-final.log` (4/4) — every tracked recipe passed under the converted working copies in the same run |
| 4 | The family sweep holds: no other file-text capture hands a parsed value a terminator, and the `// args:` line has one consumer | machine | pass | the independent review re-derived the sweep over all seven `RegexOptions.Multiline` expressions and the `.*$`-class sweeps of both test trees; `Expand-RecipeCode` binds only `{{s:key}}`/`{{n:key}}` plus `-RecipeArg` |
| 5 | The cycle's gates pass on the delivered revision | machine | pass | `format-2.log` carries `format exit=0`; `gates-1.log` (299/299, the checklist gate excluded while the checklist was being filled) and `full-1.log` (4,573 + 299, exit 0); `gates-2.log` (300/300 standalone after the fill) |

## Residuals for the user

None.

## Limits

- No client was started and no artifact was deployed: the ticket changes a test-side gate and no runtime
  behaviour, so nothing about a running session is claimed.
- Exotic line endings (mixed, CR-only) are outside the declared CRLF policy and were not judged as rows;
  the independent review measured that both the old and the new expression fail to find the declaration
  there, so the change is not a regression for them.
- The samples' discriminating leg is a reverted-matcher run on the delivered content (the expression the
  ticket's Problem section names), not a rerun of the pre-fix file revision.
