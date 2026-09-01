# check-architecture.ps1 performance

- Status: Todo
- Priority: Medium
- Category: Tooling / developer experience / build gates

`tools/check-architecture.ps1` currently takes about 90 seconds to run. It is
part of the mandatory pre-commit gate, so a slow architecture check adds notable
friction to every commit cycle.

## Goal

Reduce the effective runtime of the architecture gate without weakening its
checks.

## Scope

- Measure where the 90 seconds are spent (MSBuild / Roslyn / file/enumeration /
  PowerShell startup, etc.).
- Keep the existing hard gate behavior: ≤600-line classes, ≤5 state bools,
  one top-level type per file, plus the project-specific isolation/authority
  checks it already performs.
- Optimize the implementation (parallelism, caching, incremental analysis,
  cheaper reflection, avoid redundant solution loads, etc.) only with evidence
  that the gate still catches the same violations.
- Add a performance regression note or a simple timing assertion only if it can
  be made stable enough; otherwise document the measured improvement in the
  selfcheck/review.

## Non-goals

- Do not weaken or bypass architecture enforcement.
- Do not remove gate coverage to make the script faster.
- Do not convert this into a one-time cleanup without an actual runtime
  improvement measurement.

## Evidence / current state

- Observed during 2026-09-01 CUCoreLib migration work: `check-architecture.ps1`
  took ~90 seconds in the full gate run.
