# Step 7 — implement

Read this file and `07-implement.local.md` completely before editing source.

## Language and shape

English in code, comments and committed docs. Modern idiomatic C#: `var`, nullable, collection expressions, `is null`,
and `== null` for Unity objects. Only `GameAdapter` references game assemblies. Minimum visibility; a capability becomes
public only when it is designed, documented and reviewed.

## Localisation

Every player-visible string travels as a key plus arguments and is rendered in the reader's own language on the client
that shows it. One language's sentence never sits inside another's shell. A new refusal or result path lands with its
key and both languages in the same change.

## Files and edit tools

A tracked file is written by the edit tools, never by a shell round trip. Only gitignored local artefacts may be written
by a command.

## Tests

Cover core, edge, exception and failure paths; every key path and branch must be observable from the log with context
that locates a failure (branch, state, ids, input, result). Only the happy path is not verification.
## Details that are gated elsewhere

Prefer `using` directives and aliases; a fully qualified name only when unavoidable. Extension methods use the C# 14
`extension` syntax. Empty directories that must stay carry a 0-byte `.gitkeep`. No erased type in the mod-visible
contract (`byte[]`, `object`, `Delegate`, `dynamic`, `IntPtr`); a binary payload is an explicit leaf.

## Reports are bounded by data

A member's report is checked against a fact the receiver already holds, or the gap is named; a receiver never models
what a player is able to do (that is anti-cheat, out of MVP scope). Census:
`docs/architecture/member-report-validation.md`.

## Learning is recorded

Reusable, generalisable knowledge is recorded where it loads next time — this file or its local sibling, not only in
the conversation. A hook that reports a write reports only verified writes: re-read the state or pass the verdict
explicitly.