# Game-assembly contract snapshot, diff and update-adaptation report

- Status: Todo
- Priority: High
- Category: Verification tooling / game-update adaptation
- Source: Loomi architecture review (2026-09-20), item 2
- Related: `future/adapter-shell-verification-harness.md` (keeps the live-game half), `docs/evidence/normative-gates.md`

## Problem (evidence)

The adapter's only game-facing probe is `GameAdapter.ProbeGame()`: four `typeof(...)` reads
(`PlayerCamera`, `Body`, `PreRunScript`, `WorldGeneration`) tested with `is not null`, and a
`CapabilityReport` string with three values. For compile-time references that test cannot fail — a
type that is really gone takes the assembly load with it — so today it is effectively a constant
true.

What does exist is per-patch verification, and it is good: `PatchInventory.VerifyMissing` resolves
every `[HarmonyPatch]` target after install and `PatchContractChecker` compares the target's
argument types and the patch's parameter names (Harmony binds patch arguments BY NAME), and the
same contracts are asserted against the real game assembly by `dotnet test` before the game ever
launches. But that covers the patch TARGETS only. It cannot see a member the game's own code reads
inside a method we hook — decision 199 records exactly this residual for the pinyin patch, whose
read sits in a compiler-generated lambda no contract can name.

So the answer to "what did the game change?" is assembled today by chasing compile errors and by
whichever of the 113 patch classes fails verification first, one update at a time.

## Goal

A read-only toolchain that answers that question for two game builds, without the game running:

- **Stage 1 — snapshot.** One command writes a machine-comparable JSON snapshot of the game
  assemblies: assembly hashes, the type list, member signatures, the patch-target contract rows,
  enum members and values, and serialized fields.
- **Stage 2 — diff.** One command compares two snapshots and CLASSIFIES the differences rather than
  listing them: removed or renamed, signature changed, a new overload that makes a Harmony target
  ambiguous, field type or visibility changed, enum value changed, and "unchanged but still needs a
  semantic look".
- **Stage 3 — one source of facts.** The existing `PatchInventory` contracts feed the same snapshot,
  so the patch-target rows in a report and the standalone snapshot cannot drift apart.

## Acceptance

- Two builds produce a report, and a deliberately renamed member in a test fixture is classified
  (not merely listed) — the classification is the deliverable, not the diff.
- The snapshot is reproducible: the same input produces byte-identical output.
- The tool is read-only and never becomes a runtime dependency of the plugin; it may not reference
  the Game Adapter.
- The update-day runbook is written down: snapshot the previous build → snapshot the new build →
  diff → patch-contract tests → offline replay → compatibility report.

## Limits

The structural half is what this ticket delivers. The semantic half — "the members are all still
there, but do they still mean the same thing?" — needs the live game and stays in
`future/adapter-shell-verification-harness.md`. Neither half is a substitute for the other.
