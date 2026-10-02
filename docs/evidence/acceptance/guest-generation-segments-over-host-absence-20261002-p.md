# Acceptance record — A member's layer generation that spans the host's absence counts host-wait yields as segments

- Ticket: `guest-generation-segments-over-host-absence` — verdict: **pass; moved to `done/`** (rows 1–5)
- Batch: `20261002-p` — tickets `guest-generation-segments-over-host-absence`,
  `world-determinism-world-fingerprint`
- Commit: `8d7ffecb` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+8d7ffecbd58daea155e2bf5113a2912b1f1d0364` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-02, 23:26–23:32 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client:
  Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: `p2-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member's generation that spans the host's absence: census equals the host's, no divergence | machine | **pass** | the members' `[GenStream] done — 19 segments` against the host's own `done — 19 segments` for the same generation (`p2-guest-tail.txt`, `p2-alt-tail.txt`, `p2-host-2.txt`), and the 40 s window after the host's snapshot covers four 10 s cadences with NO `baseline divergence` or `snapshot index … disagrees` line (`p2-guest-window.txt`, `p2-alt-window.txt`). The pre-fix red for the same staging was 41 segments with the warning repeating (`20261002-p/p-guest-segments-2.txt`) |
| 2 | The decision state itself, without the host's snapshot correcting it | machine | **pass** | member `[LayerMod] guest replay index=-1 depth=0 entryState=A0B53A44061800B8A7E78EE011064AA5 afterState=061800B8A7E78EE011064AA5AD304C34` equals the host's `[LayerMod] enter state=A0B53A44061800B8A7E78EE011064AA5 chance=40 depth=0 override=None` (`p2-guest-tail.txt`, `p2-host-2.txt`) |
| 3 | Host present for the whole member generation (regression) | machine | **pass** | the entry generation: all three sides `[GenStream] done — 19 segments` with the same decision entry `A0B53A44…` (`p2-host-entry.txt`, `p2-guest-entry.txt`, `p2-alt-entry.txt`) |
| 4 | Third-party view | machine | **pass** | the alternate client's lines are byte-identical to the guest's in rows 1–3 (`p2-alt-tail.txt`, `p2-alt-entry.txt`) |
| 5 | The host's own path unchanged | machine | **pass** | the host's own generation stayed 19 segments with its decision entry and `picked=none` unchanged (`p2-host-entry.txt`, `p2-host-2.txt`) |

## What the run was

Three clients in one run (host + Steam1 + Steam2), driven in-process, all entering through the Online
UI's own controls. The absence staging repeated the red run's shape: the members left the world (staying
connected), the host left the world as well, the host continued the run (its own generation finished
23:30:43.858 with `enter state=A0B53A44…` and `World join sent to 2 member(s)` at 23:30:43.943), the
members' generation started at 23:30:45.249, and the host left the world again at ≈23:30:50 —
mid-generation. Both members finished at 23:30:58.22x, i.e. with the host absent, and both reached the
host's decision state. The host's second Continue (23:31:2x) delivered its snapshot; the members logged
`World ready — start playing` and no divergence over the following window.

## Limits

- One staging: the host leaves while the members generate. A member generation spanning a host
  *reconnect* is not staged.
- The absence covers ≈8 s of a ≈13 s generation (the host left mid-generation and did not return until
  after it finished).
- State comparisons are the 16-byte windows the runtime logs; the full `Random.State` is not captured.
- Machine facts live in `docs/acceptance/AGENTS.local.md` and the local artifact directory, never here.
