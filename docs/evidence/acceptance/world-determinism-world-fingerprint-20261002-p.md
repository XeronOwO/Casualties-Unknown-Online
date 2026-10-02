# Acceptance record — World determinism / WorldFingerprint comparison

- Ticket: `world-determinism-world-fingerprint` — verdict: **pass; moved to `done/`** (row 3; procedure
  steps 1, 2 and 4 re-confirmed)
- Batch: `20261002-p` — tickets `world-determinism-world-fingerprint`,
  `guest-generation-segments-over-host-absence`
- Commit: `8d7ffecb` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+8d7ffecbd58daea155e2bf5113a2912b1f1d0364` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-02, 23:26–23:32 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client:
  Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: `p2-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1–2 | Host + guest enter the same layer; the two `[WorldFingerprint]` lines are captured and compared | machine | **pass** | the entry pair is identical on all three clients: `4E82B57F3E3230FB 008CE5302E65B0F1 84A1A5DB40333B76 8A47535745B057F7 AF29D327B6851F90 A67EE1E59DC4459A A240549734FF4860 927FFDABAA7B9008 total EAA3F351F86C87FC` (`p2-fp-host.txt`, `p2-fp-guest.txt`, `p2-fp-alt.txt`) |
| 3 | Re-capture after a mining/placement pass | machine | **pass** | the mutation pass converged on all three clients (cell (514,512): air→gravel→air; cell (515,512): air→gravel, left in place — `p2-host-set.json`, `p2-host-break.json`, `p2-host-set2.json`, `p2-read-*-set*.json`, `p2-read-*-break.json`). The on-demand re-capture — the committed `world-fingerprint` recipe calling the product's own `WorldFingerprintLog.Log`; ledger `Acceptance.WorldFingerprint` — is identical on all three clients and differs from the entry pair exactly in the changed 128-row band and the total: `4E82B57F3E3230FB 008CE5302E65B0F1 84A1A5DB40333B76 8A47535745B057F7 3306B9EBE8EBFA3A A67EE1E59DC4459A A240549734FF4860 927FFDABAA7B9008 total 65493B72BED5EC0E` (`p2-fp2-host.txt`, `p2-fp2-guest.txt`, `p2-fp2-alt.txt`) |
| 4 | Record the result | machine | **pass** | this record |

## What the run was

The three clients entered one run; the entry one-shot lines were read from each client's own log. The
first mutation (write gravel at (514,512), then break it) nets back to the original table, so its
re-capture equaled the entry pair; the persistent write to (515,512) is the mutation the compared pair
exercises. Cell (515,512) sits in the fifth 128-row band, which is the chunk that moved
(`AF29D327…` → `3306B9EB…`) — the fingerprint localizes the change as documented.

## Limits

- One world and one mutation pass; the fingerprint is a diagnostic, not a repair path, and an automatic
  periodic comparison remains a separate future feature (the ticket says so).
- The re-capture device is the acceptance recipe; it is not part of the shipped plugin behaviour.
- Machine facts live in `docs/acceptance/AGENTS.local.md` and the local artifact directory, never here.
