# Acceptance — the mod API carries no opaque payload

- Ticket: `docs/backlog/done/mod-api-no-opaque-envelopes.md`
- Artifact: `0.1.0+566b84d4`
- Batch: none needed — the row is a CONTRACT fact read from the recorded API baseline plus the gate that
  enforces it
- Date: 2026-10-24
- Result: **the ruling's row holds** — no live member of the mod-visible contract carries `byte[]`.

## What was read

- `docs/contracts/abstractions-api-baseline.txt`: every `byte[]` occurrence (37) is a `*REMOVED*` tombstone
  naming what the two stages retired and why. No live member carries `byte[]`; the model's binary leaf is
  `ModValue.Binary(ReadOnlyMemory<byte>)` / `TryGetBinary(out ReadOnlyMemory<byte>)`, a typed member rather
  than an erased one.
- The full suite (`dotnet test CasualtiesUnknownOnline.slnx`) is green — 6357 behaviour + 779 normative
  gates — including `ApiSurfaceGateTests`, which compares the live surface against that baseline.
- The two stages' own records are the selfchecks
  `docs/evidence/selfchecks/mod-api/mod-value-model-selfcheck.md` and `mod-value-model-stage-b-selfcheck.md`,
  and the frozen shape is the user's 2026-10-09 direction recorded in the ticket.

## Limits

- This is a contract reading, not a new runtime run: the ticket's subject is the shape of the API, and the
  runtime behaviour of the model is covered by the suite's own cases (the codec, the stores, the status
  frame and the projections) and by the earlier stages' selfchecks.
- The in-tree consumers and the example mods are the platform's own; a third-party consumer remains the
  general proof the ticket never claimed.
