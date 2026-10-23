# Acceptance — the peer content-fingerprint mismatch is named, not silent

- Ticket: `docs/backlog/done/mod-content-fingerprint.md`
- Artifact: `0.1.0+6e782b7a`
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `fprow`)
- Date: 2026-10-24
- Result: **the acceptance row passes** — two clients that materialize different content under the same mod id
  and version are reported as a NAMED mismatch, and both are admitted.

## What was run

1. The built example mod `CasualtiesUnknownOnline.ModExample.dll` (`cuo.example`, `0.1.0`,
   `NetworkMode.Synchronized`) was placed in the deployed plugins directory, so every client loaded it.
2. The host's registry was mutated before hosting, through the production `ModContentStore.Add` (one extra
   registration under `cuo.example`) — the host's per-mod fingerprint for `cuo.example` became
   `a7fd32aa…`.
3. The host created a Steam lobby; the two members joined (both `active=true`).

## Result

The host logged, for BOTH members:

```
Handshake from <peer>: cuo.example (Synchronized) materializes different content than the host's copy
(42d9fc1a5694c3609a60ff877f15177f8357c60390ce3ee8655b6f565480ad2e vs a7fd32aa89dabe6ae8c5465612a598237b4840cab46382b1688528c9c1e09b9c)
— admitted; the comparison covers the address of every entry, never a computed member's value.
```

The line names the mod, both fingerprints and the mode; the members were admitted (the check is visibility,
never a refusal — the user's 2026-10-10 ruling).

## Substitutions and limits

- The differing content is a declared fixture: the example mod's own registration plus one extra registration
  written through the store's production `Add`, not two differently-built mods. The comparison path (the
  handshake, per-mod fingerprints, the host's line) is the production one.
- The example mod DLL was removed from the plugins directory after the run and the deployment re-verified
  (`0.1.0+6e782b7a`).
- The durable half (a cut records the fingerprint and a load reports a `ContentMismatch`) was not re-run; the
  selfcheck and the archive tests cover it, and the ticket's row is the peer comparison.
- No OS-level mouse or keyboard input was used.
