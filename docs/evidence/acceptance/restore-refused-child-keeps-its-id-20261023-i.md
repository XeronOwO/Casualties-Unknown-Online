# Acceptance record — a refused restore child falls to the ground

- Ticket: `restore-refused-child-keeps-its-id` — verdict: **moved to `done/`**. The refused child is dropped into
  the world and reported; it is not destroyed and no end is announced.
- Batch: `20261023-i` — the ticket's three rows.
- Commit: `52e3bde3` (the code) on `e63058f5` (a documentation commit above it) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, `ProductVersion` `0.1.0+52e3bde30c3ef5894e0d0c5830dfea6cd9f5d2c3` —
  `tools/verify-deploy.ps1` exit 0 before the clients started.
- Run: 2026-10-23 23:11 → 23:16, this session's clock · Host: physical machine · Guest: first sandbox.
- Dependencies: `preflight.ps1`, `session-environment.ps1 -Mode status`, and the in-process evaluator on both
  endpoints (the run drove the restore through it; no pointer was needed).
- Artifacts: `row1-refused2.json`, `row2-control2.json`, `host-refused.json`, `step2.json`, `step3.json`,
  `mint.json`, `marks*.txt`, `row1-guest.json` in the directory named by `acceptance-artifacts-dir`; players are
  written `<host>`/`<member>`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A restore row whose container refuses one child: the child's instance id is either placed somewhere the run can read, or its end is announced | machine | **pass** | On `<member>`, the restore of a `toolbox` (id `35262760597`) into a carried `plasticbag` was refused by the container's own rule and then DROPPED: `Restore: toolbox (id 35262760597) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.` followed by `[Restore] refused content item toolbox (id 35262760597) fell to the ground at (2.9,496.3) and its creation was reported.` — **no `OnItemDestroyed` line for that id follows**, and the host's kernel table has no terminal entry for it (`container-read mode=host`: `terminal` holds only the three ids this batch's earlier fixtures destroyed through their own condition 0). The host materialized the report as a world item: `[ItemSpawn] materializing toolbox (id 35262760597) at (2.9,496.3), vel (0.0,0.0), container 0.` |
| 2 | The same restore with every child accepted: unchanged, every child present with its id | machine | **pass** | The same forged restore with a `bandage` (id `39557727893`) into the same `plasticbag` wrote **no** refusal and **no** drop line, and the child loaded into the container on its own client: `[ItemTrace] op=13 item=39557727893 origin=OnItemLoadedIntoContainer result=Committed(1) events=[Pickup]` (`marks4.txt` window). |
| 3 | A world item's contents restored on the receiving client with one child refused: the host's item table does not lose the id without an announced end | machine | **pass** | Read with row 1's own outcome: the reported spawn is materialized by the host as a world item (`[ItemSpawn] materializing … container 0`) and the host's terminal set does not gain the id. The container is a character-carried one in this run, not a world item's (see Limits). |

## What the run had to substitute, and what it taught

The ticket calls the setup "forge the archive or the container's limit". The run forged the SNAPSHOT ENTRY instead:
a batch-local probe (`restore-refused-child.cs` / `step3.cs`) mints an id through the client's own
`ItemIdAllocator`, builds the `CharacterItemMsg` a restore would carry, and hands it to the same
`ItemStateCodec.RestoreContents` the character and world restores call. The container is a created `plasticbag`
carried by the client, the child a `toolbox` it cannot hold.

Two findings from the staging, both recorded rather than patched:

- **A condition-0 entry is destroyed by the game itself.** The first attempt left `CharacterItemMsg.Condition` at
  its default `0`; the dropped object was destroyed ~60 ms after creation on BOTH clients (`[ItemTrace] op=5
  origin=OnItemDestroyed result=Committed(1)`), independent of the policy. That is the game's own broken-item
  path, not the restore's: with `Condition = 1` the same drop survives. A forged fixture must carry a real
  condition or it measures the game's item destruction instead of the restore policy.
- **A locally-minted id is not a host-known aggregate.** With an id the host never minted, the guest's own
  `ItemPickup` command for the accepted child is refused by the host (`UnknownAggregate`) — a property of the
  forgery, not of the policy. A real restore carries an id the host already holds.

## Residuals for the user

None.

## Limits

- **Character-carried container, not a world item's.** Rows 1 and 2 read the bulk restore on a container the
  client carries; row 3's "world item's contents" is the same call (`ItemStateCodec.RestoreContents` →
  `RestoreContentDropping`) with a different container owner, and was not staged separately — the host-table half
  is read from row 1's outcome.
- **The forged id is client-minted**, so the host treats the item as an aggregate it never had: the spawn is
  materialized, but the run does not claim the host would have refused or adopted it in a real restore. What is
  read is the policy's own outcome (no destroy, a reported creation) and the absence of an announced end.
- **One item pair, one refusal reason** (a container's own weight rule). Other refusal paths (a slot collision, a
  tag restriction) reach the same policy and were not staged.
- **The first attempt's red is kept**: the condition-0 fixtures are cited above as the reason the fixture carries
  a condition, and their ids are the terminal entries this record names.
