# Mod API contract governance: visibility rule, stability levels, public-surface baseline

- Status: Todo
- Priority: Medium-High
- Category: Mod API / compatibility governance
- Source: Loomi architecture review (2026-09-20), items 7 and 8 and the visibility discussion
- Related: `docs/api/mod-api.md`, `docs/decisions/active.md` (protocol numbering policy)

## Problem (evidence)

`src/CasualtiesUnknownOnline.Abstractions/` carries 90 public type declarations and is the only
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

## Notes

Deliberately NOT in this ticket: splitting `Abstractions` into several assemblies, and building the
full Mod SDK/tooling set (that stays in `future/phase5-tooling-ecosystem.md`). Both are real, both
have no trigger yet, and both would be paid for now and used later.
