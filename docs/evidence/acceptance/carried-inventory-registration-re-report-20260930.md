# Acceptance record — Carried-inventory registration has no re-report

- Ticket: `carried-inventory-registration-re-report` — verdict: **stays in `review/`** (rows 1–2 pass;
  row 3 could not be judged: the rejoin never re-activated the session)
- Batch: `20260930-f` — the container-scenario session shared with
  `guest-container-contents-ghost-drops-on-host`
- Commit: `29b59c07` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+29b59c07bebc80ad6c612169e7bed266751087ff` (deploy and `verify-deploy.ps1` exit 0). The container
  recipes were corrected by this run and committed as `40322b6b` — tooling only, the deployed product
  build is unchanged
- Run: 2026-09-30 20:54 → 21:17 (+08:00) · Host: physical machine (evaluator `18590`) · Guest: primary
  sandbox (evaluator `18591`; relaunched once for the reconnect half) · Third peer: alternate sandbox
  (evaluator `18592`)
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON and the three clients'
  logs, cited below by name

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest starting inventory; registration dropped → host converges; take arbitration works | machine | **pass** | At 21:02:16 the host's per-guest entry map was removed through the evaluator (`r1-00-clear.json`: 2 entries → 0). Both other clients stayed idle; reads at 21:02:20 / :26 / :33 / :40 returned 0 entries, and the read at 21:02:46 returned the same two ids again (`r1-01`…`r1-05-read.json`), each entry `isTransferred=true` with kernel location `Carried` (`r1-05`). The host log carries exactly one registration line for that step — `Registered 2/2 carried items … (0 already registered, 0 refused by the kernel)` — i.e. the registration path rebuilt the empty record; the kernel-rebuild path logs at Debug and is absent |
| 2 | An id added after the first report → converges on the next report | machine | **pass** | The carried set grew after the first report: the guest picked a world trash bag up (slot 0) and a created dog food (slot 1), then loaded the dog food into the bag — the guest log names `[PickUpResult] trashbag → slot (slot 0)`, `[PickUpResult] dogfood → slot (slot 1)` and `[ContainerLoad] dogfood … moved inside body container trashbag — root content event up to trashbag …`; `i3-fill-container.json` reads `contentsAfter=1`. After the clear, the next report re-registered the bag **with its content**: the restored entry's `contents` holds the dog food id (`r1-05`) |
| 3 | Reconnect while in world → table rebuilt exactly once | machine | **unproven (rejoin setup)** | The offline clear emptied the map (`r2-01-clear-offline.json`: 2 → 0). The relaunched client rejoined the same lobby, but its session never activated (guest facts `active=false, members=[]`, scene `PreGen`; the guest log repeats `Retrying handshake` for the whole process life) and the host's member list never regained it, so no join-edge re-registration happened: `r2-02-host-table-after-rejoin.json` read an empty table |

## Limits

- The swallowed-registration precondition is the substitution the batch scope page declares: the host's
  per-guest entry map is removed through the evaluator instead of a real dropped frame. Two controls back
  the attribution — both other clients stayed idle for the whole observation window, so no guest batch
  could trigger the kernel's own rebuild path (`ItemService.OnExternalBatchCommitted`), and the host log's
  single registration line lands inside that window.
- "Arbitrate" is evidenced by the restored entries and by `ItemArbitration.IsTransferredToGuest`
  answering true for each id — the guard the arbitration seam asks. A cross-player take was not exercised:
  the ordinary take path refuses a conscious source, and the remote-backpack transfer needs the native
  drag this run does not drive.
- The registration report rides the dense window (5 s steps) and then the steady re-assertion; the world's
  dense window closed long before the clear, so row 1's convergence came from a steady step (the guest log
  shows the dense window opening at world entry: `[CarriedInventory] reported 1 carried item(s) …`,
  archived first-process log).
- Row 3's rejoin gap: the documented relaunch → wait for the endpoint → rejoin steps left the session
  inactive (host member list unchanged; the guest's handshake retries unanswered). The reconnect
  capability needs its own verified checkpoints before the row can be judged; this run does not claim the
  table rebuilds on reconnect.
