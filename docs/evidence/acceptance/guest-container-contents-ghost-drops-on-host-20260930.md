# Acceptance record — Guest carried container contents appear as world drops on the host view

- Ticket: `guest-container-contents-ghost-drops-on-host` — verdict: **stays in `review/`** (rows 1–2 pass;
  row 3 could not be judged: the rejoin never re-activated the session)
- Batch: `20260930-f` — the container-scenario session shared with
  `carried-inventory-registration-re-report`; the reconnect half of the batch is the sibling record's
  finding
- Commit: `29b59c07` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+29b59c07bebc80ad6c612169e7bed266751087ff` (deploy and `verify-deploy.ps1` exit 0). The container
  recipes were corrected by this run and committed as `40322b6b` — tooling only, the deployed product
  build is unchanged
- Run: 2026-09-30 20:54 → 21:17 (+08:00) · Host: physical machine (evaluator `18590`) · Guest: primary
  sandbox (evaluator `18591`; relaunched once for the reconnect half) · Third peer: alternate sandbox
  (evaluator `18592`)
- Dependencies: preflight `11 present`, exit `0` (`steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`,
  `dotnet`, `capture`, `input`, `logs`, `artifacts`); machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON, frames and the three
  clients' logs, cited below by name

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | No dog food appears as a world drop on the host while the guest carries the trash bag | machine | **pass** | The host's world-item id set is identical across the three movement windows and all six reads (262 ids, one combined hash; `h0-host-authority-before-move.json`, `h1-host-authority-after-move.json`, `h2a/h2b`, `h3a/h3b`): the nested id never enters the world half, the contained half stays one entry whose parent is the bag in every read, and the terminal half stays empty. Guest travel per window ≈ 15 / 11 / 8 world units (`m1-*`…`m5-*` slide files) |
| 2 | The guest view and the host view show the dog food inside the container consistently | visual | **pass — frames read** | Guest's own tree (`i4-guest-local-after.json`): the bag with one dog food child and non-zero ids; host's rendered proxies (`b1-host-clone-after-fill.json`, `b2-host-clone-after-move.json`): the dog food proxy under the bag proxy, `underClone=true`, `orphanCount=0`. Frames `f1-guest-container-panel.png` / `f1-host-container-panel.png` and their zooms `f2-guest-container-strip.png` / `f2-host-container-strip.png` were read by the agent: both native container windows show the bag sprite at `1.48/4u` holding one dog food can |
| 3 | No duplicate or ghost item id, no transfer-table resurrection, no dropped item after reconnection | machine + visual | **unproven (rejoin setup)** | The relaunched client rejoined the lobby, but its session never activated, so no reconnect state was reachable: guest session facts `active=false, members=[]`, scene `PreGen`, no world params; the host's member list held only the third peer (`snippet-session.cs` reads). The host's carried-id table stayed empty after the offline clear (`r2-01-clear-offline.json`, `r2-02-host-table-after-rejoin.json`). The ticket stays in `review/` |

## Residuals for the user

- None: every row this run could decide is machine-judged or agent-read from a frame.

## Limits

- Row 1 is judged by id-set identity in the host's authoritative world-item table across repeated movement
  windows, not by a frame-by-frame violation count; one session cannot disprove a rare race.
- The item pair came from the run, not from the game's starting-supply grant (the run's `startingsupplies`
  setting is `light`, so the guest body carried only the emergency light): a world trash bag was created
  through the game's own factory and picked up through the native pickup guards, and a dog food was created
  the same way and loaded into the bag through `Container.LoadItem`. The guest log names both pickups and
  the container load.
- The host's remote container window needed line of sight to the guest: the first open was refused
  (`b3-host-panel-open.json`, `open-refused`), the run moved the host three slide windows until the
  registered remote-backpack entry accepted it (`b4-host-panel-open.json`, `opened=true`), and both panels
  were opened through the game's own window entry, never by input.
- Row 2's visual half cannot resolve the world at the game's native zoom; the panel frames are the
  deciding visual evidence and the proxy/local reads are the machine evidence.

## Mechanism note

The ticket's *Root cause and fix* section names a `CloneInventoryContentSanitizer` wired into
`CloneInventoryRenderer.RestoreRemoteContents`. In the frozen tree that sanitizer type has no production
caller, and `RestoreRemoteContent` attaches the display-domain `RemoteInventoryItemId` marker, never a
domain instance id; the live defence this run exercised is the display-proxy skip in
`RemoteItemSceneOps.FindWorldItem` / `FindExistingAt`. The row verdicts above therefore rest on the
observable behaviour, not on the sanitizer claim.
