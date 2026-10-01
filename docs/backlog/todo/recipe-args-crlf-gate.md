# Recipe `// args:` gate and CRLF working trees

- Status: Todo
- Priority: Medium
- Category: Tooling / normative gates
- Source: found on 2026-10-01 while adding the capability batch `20261001-y` recipes: a recipe file
  written with CRLF line endings fails `AcceptanceDriverGateTests`.

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

## Evidence

- Observed (capability batch `20261001-y`, this machine): a CRLF copy of
  `tools/acceptance/recipes/block-damage-fill.cs` fails the gate; the same file as LF passes 4/4.
- `git ls-files --eol tools/acceptance/recipes/` - `i/lf w/lf attr/text=auto eol=crlf` on every
  TRACKED recipe; the batch's five new files (`block-damage-fill`, `block-damage-census`, `crush-find`,
  `item-floating`, `item-watch`) are LF on disk as well and are committed as LF, so the next checkout is
  the first place the disagreement this ticket names can actually bite.
- `.gitattributes` - `* text=auto eol=crlf`; `.editorconfig` - `end_of_line = crlf`.
