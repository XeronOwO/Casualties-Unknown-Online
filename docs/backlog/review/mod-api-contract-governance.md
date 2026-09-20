# Mod API contract governance: visibility rule, stability levels, public-surface baseline

- Status: Review
- Priority: Medium-High
- Category: Mod API / compatibility governance
- Source: Loomi architecture review (2026-09-20), items 7 and 8 and the visibility discussion
- Related: `docs/api/mod-api.md`, `docs/decisions/active.md` (protocol numbering policy)

## Problem (evidence)

`src/CasualtiesUnknownOnline.Abstractions/` carries 90 public type declarations before this change
(92 after it, counting the two stability-marker types it adds) and is the only
assembly a mod may reference, but nothing in the tree says which of them is a compatibility
promise. `docs/api/mod-api.md` §5 says dependency version ranges are "deliberately not inferred
until a formal API-compatibility contract exists" — that contract is the missing artifact.

Three concrete gaps:

- **No stability level.** `IModContext` is one wide facade over roughly twenty capability
  interfaces (`IModNetwork`, `IModCommands`, `IModState`, `IModData`, `IModContent`, `IModGameState`,
  `IModNativeApi`, and the rest), so an author cannot tell a frozen surface from one that will move,
  and `AccessNativeApi` — the most game-version-sensitive escape hatch — carries the same implicit
  promise as the core.
- **No baseline.** Adding a public member is free, and nothing records what the surface was, so the
  first release would freeze 90 types by accident rather than by decision.
- **No policy for authors who patch CUO itself.** The review's answer — contracts public,
  implementations internal, Harmony allowed but never promised — is not written down anywhere, so
  the next contributor has to re-derive it.

One drive-by defect found while checking this area: `docs/api/mod-api.md` §7 still says
`ProtocolVersion.Current` is `31`, while `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs`
declares `34` and the last three decisions record 34. If the refusal contract cannot keep a single
integer straight in prose, the baseline above is the mechanism that catches it next time.

## Goal

- One rule in `AGENTS.md`: types and members default to the minimum visibility the implementation
  needs; only a capability that is designed, documented and reviewed becomes a public third-party
  contract. Implementations may be Harmony-patched, and that carries no compatibility promise.
- A stability marker for public API — Stable / Experimental / Advanced / Obsolete — as a namespace
  or an attribute, decided in the change that first needs an Experimental surface; no assembly split
  is required yet.
- A public-surface baseline gate: the recorded `Abstractions` public types, members and signatures;
  an addition fails until it is explicitly approved, a removal of a Stable member fails outright.
- `docs/api/advanced-modification-policy.md`: Harmony is allowed and not treated as hostile;
  Runtime/GameAdapter are not contracts; the diagnostics an author can expect; and the promotion
  funnel — patch → several mods need the same thing → Experimental API → Stable API.
- Fix the §7 protocol-number drift and keep the number in one place.

## Acceptance

- The gate is red on a synthetic added public member and green on the current tree; the baseline
  file is committed and its census is asserted (a gate that scans nothing must not pass).
- `docs/api/advanced-modification-policy.md` exists and is linked from `docs/api/mod-api.md`.
- The `AGENTS.md` rule is added as one binding line, not a section.

## What landed (2026-09-20)

Self-check: `docs/evidence/selfchecks/tooling/mod-api-contract-governance-selfcheck.md`; decision 203.

- **The rule.** `AGENTS.md` Engineering Conventions gains one binding item (minimum visibility, only a
  designed/documented/reviewed capability is a public contract), and
  `docs/api/advanced-modification-policy.md` defines contract vs implementation, the four stability
  levels, the Harmony policy (allowed and not treated as hostile, and it buys no promise), the
  diagnostics an author can rely on, and the promotion funnel patch → several mods need the same thing
  → `Experimental` → `Stable`. `docs/api/mod-api.md` links it from §1 and its §5 no longer says the
  API-compatibility contract is missing.
- **The stability marker.** `ApiStabilityLevel` + `ApiStabilityAttribute` in `Abstractions`. `Stable`
  is what the ABSENCE of the attribute means, so the baseline records the effective level and the
  baseline review is the decision point. Two surfaces are `Advanced` with their evidence:
  `IModNativeApi` (a curated registry whose available operations follow the adapter's registration) and
  `IModNativeLocalPlayerState` (a projection of the game's own body fields). The marker is an
  attribute rather than a namespace: one assembly, no `using` churn for 90 types, visible in
  IntelliSense.
- **The baseline + gate.** `docs/api/abstractions-api-baseline.txt` is the reviewed record (769 lines:
  13 header + 92 type + 664 member entries) of the ONLY assembly a mod may reference.
  `ApiSurfaceGateTests` re-derives that surface from the project's 92 sources with Roslyn and fails on
  an addition, a changed line (signature, declaration modifier, accessor, default value or stability
  level), a removal without a `*REMOVED* <key> — <reason>` tombstone, a tombstone whose entry came
  back, a malformed or duplicated line, an `[ApiStability]` argument it cannot resolve, and a
  declaration kind it does not model; the census floor is asserted on BOTH sides (60 files / 54 types /
  450 entries against a measured 92/92/756). The extraction models what a caller can reach, including
  the C# 14 `extension` block (the receiver is folded into each member's parameter list), primary
  constructors, and an identical `partial` declaration split across files. The failing gate writes the
  candidate to the gitignored `artifacts/api-surface/`; approval is copying the reviewed lines into the
  baseline, so no test writes a tracked file.
- **Behaviour preserved.** The only production edit is how one log message's text is composed
  (`AdapterCapabilityReporter`, byte-identical rendering: one LF, not `Environment.NewLine`); no wire,
  protocol-number or save change. Wire and save are untouched.
- **Pre-existing red cleared, and the class of miss closed.** `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths`
  was RED at HEAD `bc606a4f`: the capability report's one-line log template carried a literal
  colon-backslash-n, which that gate reads as a drive-letter path (the false-positive shape its own
  documentation warns about, and the gate code and that line are unchanged from HEAD). The text is
  composed instead, so the gate is strict again and the rendered message is unchanged. The root cause
  of both that miss and this cycle's own review blocker is the gate enumerating `git ls-files` (tracked
  files only), so it now scans `--cached --others --exclude-standard`: a brand-new file is checked
  before the commit that adds it.
- **Independent adversarial review.** Run against the frozen tree before the commit (1 blocker /
  2 major / 6 minor / 3 nit); all fixed in this cycle — the blocker was the new gate's own source
  re-creating the same drive-letter shape (fixed by `AppendLine` AND by the widened scan above), and
  the majors were the invisible C# 14 `extension` member and the unrecorded declaration modifiers. The
  review round and the unchanged remainder are recorded in the self-check.

## Notes

Deliberately NOT in this ticket: splitting `Abstractions` into several assemblies, and building the
full Mod SDK/tooling set (that stays in `future/phase5-tooling-ecosystem.md`). Both are real, both
have no trigger yet, and both would be paid for now and used later.
