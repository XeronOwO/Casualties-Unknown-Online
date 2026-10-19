# Acceptance record — Guest frame rate lower than host with frame drops

- Ticket: `guest-frame-rate-lower-than-host` — verdict: **moved to `done/`**
- Batch: `20261021-a` — tickets `guest-frame-rate-lower-than-host`, `give-item-to-another-player`
- Commit: `fd6e8e00` · Deployed artifact: `CasualtiesUnknownOnline.dll`
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546` — `tools/verify-deploy.ps1` exit 0 before the clients
  started; the commits above the build commit are documentation only
- Run: 2026-10-19 20:03:48 → 20:10 by the batch's clock · Host: physical machine · Guest: sandbox
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` — preflight eleven present, `RESULT: OK - a full two-client run is possible`
- Artifacts: `f1-*` and `f1b-*` in the directory named by `acceptance-artifacts-dir`; Steam ids are written
  `<host>`/`<member>`

The ticket's last unjudged row is the host-vs-guest comparison itself. `Diagnostics:LatencyInstrumentation`
was set to `true` in the host's and the member's own config before either client started, and restored to
`false` when the session closed. The world was otherwise idle: both clients in one run, no third client, no
gesture driven inside either measurement window.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| F1 | With `Diagnostics:LatencyInstrumentation=true`, the host and the guest each write their own `[Latency] Frame` lines and the two distributions are compared over the same quiet interval | machine | **pass** | `f1-comparison.txt`, `f1-host-latency.txt`, `f1-guest-latency.txt` — 45/46 lines each over 20:04:48→20:05:33; `f1b-comparison.txt`, `f1b-host-latency.txt`, `f1b-guest-latency.txt` — the same over 20:05:46→20:06:31. The raw lines are the deployed client's own `[Latency] Frame: calls=… total=… avg=… max=… slow=…` records on both sides. |

## The comparison

| Window | Client | Lines | Mean calls/s | Mean avg ms | Mean of per-line max ms | Worst max ms | Slow frames |
|---|---|---|---|---|---|---|---|
| 20:04:48→20:05:33 | host | 45 | 63.13 | 6.96 | 11.82 | 41.87 | 1 (in 1 interval) |
| 20:04:48→20:05:33 | guest | 46 | 69.26 | 3.97 | 5.59 | 8.19 | 0 |
| 20:05:46→20:06:31 | host | 45 | 69.93 | 6.24 | 10.73 | 36.67 | 1 (in 1 interval) |
| 20:05:46→20:06:31 | guest | 46 | 75.65 | 3.68 | 5.26 | 9.31 | 0 |

`calls` is the number of `GameAdapter.Update` pump invocations in the one-second aggregate, so the mean is
the CUO update pump's own cadence; `avg`/`max` are that pump's cost. In both windows the guest's pump ran
MORE often and cost LESS than the host's, and the guest counted no slow frame while the host counted one.
The reported symptom — a guest frame rate lower than the host's — did not reproduce on this deployed
artifact in this session.

## Residuals for the user

None.

## Limits

- **One machine, two clients, one world.** The guest is sandboxed; the two game windows shared one desktop
  and neither window's foreground/occlusion state was normalised. The game sets
  `UnityApplication.runInBackground = true`, so both kept rendering, but a window-state difference remains a
  separate variable. No third client was present, so no third-peer view was compared.
- **The line measures CUO's pump, not the engine frame.** `[Latency] Frame` brackets `GameAdapter.Update`;
  it does not include rendering, physics or the rest of the game's frame. The comparison is valid for the
  CUO overhead question, not for a GPU or whole-engine FPS claim.
- **Two 45 s windows are the repetition this run made.** A long-session drift, a layer change, heavy
  gameplay or a rejoin were not measured; the historical report's exact client and environment were not
  reproduced.
- **The configuration was changed for the run and restored.** The deployed DLL is unchanged; the record's
  numbers stand on the artifact identity above.
