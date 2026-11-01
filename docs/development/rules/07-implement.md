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
