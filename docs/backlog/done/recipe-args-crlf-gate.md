# Recipe `// args:` gate and CRLF working trees

- Status: Done
- Acceptance (20261002-a): five rows pass on the delivered revision — the focus gate is green on LF and on byte-verified CRLF working copies, the reverted matcher fails the samples, the gate project is 300/300 and the full suite with build is green.
- Priority: Medium
- Category: Tooling / normative gates
- Source: found on 2026-10-01 while adding the capability batch `20261001-y` recipes: a recipe file
  written with CRLF line endings fails `AcceptanceDriverGateTests`.
- Acceptance record: `docs/evidence/acceptance/recipe-args-crlf-gate-20261002.md`.

## Problem

`AcceptanceDriverGateTests.TheRecipesStayInTheEvaluatorsLanguageAndDeclareTheirArguments` reads the
declaration with `^// args: (?<args>.*)$` under `RegexOptions.Multiline`. In .NET `$` matches before
the `\n`, so on a CRLF line the capture keeps the `\r`: the last argument then reads `dmg=n\r`, which
is neither `<key>=n` nor a declared placeholder, and the gate fails with
`the declared argument 'dmg=n<CR>' is not <key>=s or <key>=n`.

The repository declares CRLF working trees - `.gitattributes` carries `* text=auto eol=crlf` and
`.editorconfig` carries `end_of_line = crlf` - while every file under `tools/acceptance/recipes/` is
LF in both the index and the working tree (`git ls-files --eol tools/acceptance/recipes/` reports
`i/lf w/lf`). The gate is therefore green only because the recipes were added as LF; a fresh clone or
a `git checkout` of those paths writes them CRLF, and the gate then fails on every recipe that
declares an argument. A line ending is not an argument declaration, so the gate is the side that
disagrees with the repository's own rule.

## Fix direction (decide in the cycle)

- Make the gate line-ending agnostic: capture the declaration as `(?<args>.*?)\r?$` (or trim the
  capture) and pin it with a CRLF positive sample beside the existing LF samples, so either checkout
  stays green; or
- declare the recipe directory LF in `.gitattributes` with its own rule, keeping the gate as it is
  and accepting that the directory deviates from `end_of_line = crlf`.

The first direction keeps the line-ending policy honest and matches the gate's own declaration rule
(a line ending is not a declaration error); the second is smaller but writes a directory-wide
exception into the policy.

## What landed (2026-10-02)

Direction one from *Fix direction* — the gate reads either checkout identically — with the repository's
CRLF policy left as it is. The landed form is a character class rather than the `(?<args>.*?)\r?$` the
section above proposed: `[^\r\n]*` states the rule directly — the line's content up to its terminator —
and does not depend on where `$` matches.

- **The capture is line-ending agnostic.** `RecipeProblems` reads the declaration with
  `^// args: (?<args>[^\r\n]*)` under `RegexOptions.Multiline`: the capture stops at the line
  terminator, so a CRLF checkout yields the same arguments as an LF one instead of ending the last
  one in a `\r`.
- **CRLF samples pin it beside the LF ones.** A positive with a trailing newline, a positive without
  one, a negative that must still report an invalid kind, and a `none` declaration that must still be
  accepted: the fixed matcher passes all four, and the reverted matcher fails the positives
  (`20261002-a/red-samples-final.log`).
- **The family was swept, not just this file.** No capture reachable from repository text hands a
  parsed value a terminator: `PreflightToolHarness.ReadRow`'s raw `(?<detail>.*)$` capture does keep
  the `\r`, and `.Value.Trim()` removes it before the value is used, while its `BlockingIds` `[^;]+`
  keeps one in an id-list value that is never parsed as a token; the `SourceShapeGateTests` and
  `RepositoryGateTests` expressions are `\s*`-tolerant or anchor on digits. The `// args:` line also
  has exactly one consumer — this gate — because `drive-in-process.ps1` binds only the
  `{{s:key}}`/`{{n:key}}` placeholders and the caller's `-RecipeArg`.
- **Evidence (batch `20261002-a`, offline).** All 36 recipe working copies were converted to the CRLF
  the attributes declare (byte-verified: no lone CR in the backups, pure CRLF after), and the focus
  gate ran red on the pre-fix matcher (`red-focus-crlf.log`), green on the same CRLF tree once fixed
  (`green-focus-crlf-final.log`), and green on the LF tree (`green-focus-lf-final.log`); the recipes
  were restored byte-for-byte afterwards. `dotnet format` exits 0 with the exit code in its log
  (`format-2.log`).

Mechanism × change × evidence (the cycle's self-check):

| Mechanism | Change | Evidence |
|---|---|---|
| The `// args:` declaration read | capture stops at the line terminator | `red-focus-crlf.log`, `green-focus-crlf-final.log`, `green-focus-lf-final.log` |
| The matcher's teeth | two CRLF positives, one negative, one `none` boundary sample | `red-samples-final.log` (the reverted matcher fails the positives) |
| The declared CRLF working tree | simulated by byte conversion, restored after | `recipes-eol-before.txt`, `recipes-eol-crlf.txt`; no lone CR, pure CRLF |
| The test trees' other file-text captures | swept; none hands a parsed value a terminator | `PreflightToolHarness.ReadRow` (trims), `SourceShapeGateTests`, `RepositoryGateTests` |
| The driver's argument contract | unchanged; it binds placeholders, not the declaration | `drive-in-process.ps1` `Expand-RecipeCode` |

## Evidence

- Observed (capability batch `20261001-y`, this machine): a CRLF copy of
  `tools/acceptance/recipes/block-damage-fill.cs` fails the gate; the same file as LF passes 4/4.
- `git ls-files --eol tools/acceptance/recipes/` - `i/lf w/lf attr/text=auto eol=crlf` on every
  TRACKED recipe; the batch's five new files (`block-damage-fill`, `block-damage-census`, `crush-find`,
  `item-floating`, `item-watch`) are LF on disk as well and are committed as LF, so the next checkout is
  the first place the disagreement this ticket names can actually bite.
- `.gitattributes` - `* text=auto eol=crlf`; `.editorconfig` - `end_of_line = crlf`.
