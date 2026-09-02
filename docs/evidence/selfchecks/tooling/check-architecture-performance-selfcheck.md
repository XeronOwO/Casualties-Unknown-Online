# check-architecture.ps1 performance — self-check (2026-09-01)

Backlog item `check-architecture.ps1 performance`: the mandatory architecture
gate took ~90s in an earlier full-gate run. This cycle measures the current
bottleneck, removes the dominant cost, and verifies the gate still catches the
same violations.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Full gate before | `tools/check-architecture.ps1` on the current tree: **32.42s** |
| Sub-checks after main scan | `check-gamestate-isolation`, `check-item-authority`, `check-no-legacy`, `check-command-authority`, `check-kernel-shape` totaled **~0.84s** |
| Main scanner | 1150 `.cs` files under `src/`; ~86,683 lines |
| Dominant cost | Per-line brace counting used `$line.ToCharArray() \| Where-Object { ... }` twice; isolated run of that loop: **24.48s** |
| Replacement | Count braces with `Split('{')` / `Split('}')`; isolated run: **0.16s** |
| Read path | `Get-Content` per file was ~1.53s; replaced with `[System.IO.File]::ReadAllLines` + `ReadAllText` (~0.15s combined) |

## 2. Change

`tools/check-architecture.ps1`:

- Uses `Get-ChildItem -File` and `[System.IO.File]::ReadAllLines` / `ReadAllText`
  instead of `Get-Content` for the source scan.
- Counts `{` / `}` with `Split('{').Length - 1` / `Split('}').Length - 1`.
  This is the same character-count semantics as the previous char-pipeline code:
  every brace character in the trimmed line is counted, string/comment contents
  are still ignored by design (the gate is intentionally a naive scanner).
- No check is weakened: the one-top-level-type rule, logical line aggregation,
  bool-flag rule, debt ledger, strict mode, and all sub-gate invocations remain
  unchanged.

## 3. Verification results

| Evidence | Result |
|---|---|
| `tools/check-architecture.ps1` (current tree) | Passed, **2.15s** after optimization |
| Temporary file with two top-level types | Gate failed with `2 top-level types (rule: one per file)` |
| Temporary type with six `private bool _...` fields | Gate failed with `6 boolean state fields (max 5...)` |
| Temporary files removed | `src` left clean |
| Sub-gates | Still invoked and passed in the same run |

## 4. Verification design

- Before optimizing, the isolated brace-counting loop was timed separately so
  the fix is tied to measured evidence, not guesswork.
- After optimizing, the full gate was run on the real tree to confirm the clean
  path still passes.
- Two controlled negative tests were run through the updated script: a
  multi-top-level-type file and a bool-flag overflow file. Both were rejected and
  then removed.
