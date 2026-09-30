# Acceptance record — Guest carried container contents appear as world drops on the host view

- Ticket: `guest-container-contents-ghost-drops-on-host` — verdict: **stays in `review/`**
  (rows 1–2 stand from batch `20260930-f`; row 3 `unproven` again — the reconnect now
  activates the session, but the carried container's contents did not survive the
  re-entry and the attribution is not decided)
- Batch: `20260930-g` — the reconnect single-point session shared with
  `carried-inventory-registration-re-report`
- Commit: `7c4da9df` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+7c4da9df802612e66bdd9ac1e8efe71ba6c4b43a` (deploy and `verify-deploy.ps1`
  exit 0)
- Run: 2026-09-30 23:05 → 23:19 (+08:00) · Host: physical machine (evaluator `18590`) ·
  Guest: primary sandbox (evaluator `18591`; relaunched three times) · Third peer: not
  used in this batch
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON and the two
  clients' logs, cited below by name

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | No dog food appears as a world drop on the host while the guest carries the trash bag | machine | not re-run — batch `20260930-f` verdict stands | — |
| 2 | The guest view and the host view show the dog food inside the container consistently | visual | not re-run — batch `20260930-f` verdict stands | — |
| 3 | No duplicate/ghost item id, no transfer-table resurrection, no dropped item after reconnection | machine + visual | **unproven** | The reconnect itself completed three times (both ends show the peer in the Steam lobby, session state `k_ESteamNetworkingConnectionState_Connected`, end-to-end handshake, world entry; `probe-lobby-p2p.cs` readings), so the batch-f blocker did not reproduce. The carried container did not survive the re-entry, and the run cannot attribute the loss: after the declared offline clear (`r1-00-clear-offline.json`, 2 → 0 entries) the guest's body held no capturable items (`r1-01-guest-local-after-rejoin.json`, `localCount=0`; the scene listing found only an unheld light object) while the host's kernel listed light + bag + dogfood as flat `Carried` entries and the contained relation was gone (`r1-03-host-tables-after-rejoin.json`, `containedCount=0`; the bag entry's `contents` empty). The guest log of that cycle shows the restore applying, then the restored pickups refused by the host (`Kernel command rejected by host … Conflict`). In the follow-up control reconnect (table left populated) the bag and light returned into the body slots but the dogfood was still missing and the record still had empty contents (`r2-01-guest-local-control.json`). The control started from a table already degraded by the first cycle, so the exact mechanism stays open and the row is not judged. |

## Residuals for the user

- None: every reading this run could decide is machine- or log-judged.

## Limits

- No third peer in this batch; the batch-f batch-d/e third-party view is not re-created.
- The control reconnect started from a table already flattened by the first cycle, so it
  cannot separate the restore path from the kernel rebuild path.
- The clear is the declared substitution for a swallowed registration
  (`docs/evidence/acceptance/20260930-e-scope.md`); this run shows it also removes the
  host's restore input, which is why the container did not come back in that cycle.
- One session does not disprove a rare race; the batch-f P2P wedge remains an
  unreproduced observation.

## Mechanism note

The ticket's *Root cause and fix* section is corrected in this change: the
`CloneInventoryContentSanitizer` type has no production caller in the frozen tree, and the
renderer marks each materialized clone child with the display-domain
`RemoteInventoryItemId`, never a domain instance id. The live defence exercised by rows
1–2 is the display-proxy skip in `RemoteItemSceneOps.FindWorldItem` / `FindExistingAt`
(and the sibling guards under `RemoteCloneRender`), not the sanitizer.
