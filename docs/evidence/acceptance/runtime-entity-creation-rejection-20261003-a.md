# Acceptance record — Runtime entity creation the host cannot represent must be REJECTED (and the creator's copy destroyed)

- Ticket: `runtime-entity-creation-rejection` — verdict: **moved to `docs/backlog/done/`** (every row of the
  acceptance matrix passes in this batch)
- Batch: `20261003-a` — tickets `runtime-entity-spawn-backfill`, `runtime-entity-creation-rejection`
- Commit: `3b1da7da` (artifact; the deployment this run observed) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+3b1da7dab526a2260921e4beb58a3ee664ad0e68`
  (`tools/verify-deploy.ps1` exit 0) · The commit that carries this record is one commit later (docs-only);
  the deployed assemblies do not move with it.
- Run: 2026-10-03 00:14 → 00:25 (+08:00) · Host: physical machine · Guest: Steam1 sandbox · Third client:
  Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (11 present,
  exit 0); `session-environment -Mode status`: `active=cuo`, `game-running=false`, `swap-needed=false`.
- Artifacts: `a0-*` … `a9-*`, `b1-*` … `b9-*` in the directory named by `acceptance-artifacts-dir`;
  this record cites artifact ids only.

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host lacks the prefab; guest creates and reports — host records nothing, relays nothing, logs the mismatch; no third-party materialization | machine | **pass** | `a2-guest-spawn-beacon1` created `acceptance.beacon` (`76561199526807662:3222325306`); the host logged `cannot create acceptance.beacon … Utils.Create threw (missing prefab or template).` and `REJECTED acceptance.beacon … this host cannot materialize the prefab, so the creation is neither recorded nor relayed.` (`a2-log-host`); the host's accepted table stayed `0` (`a2-tables-host`) and the third client's census stayed empty (`a2-census-alt`, its log window matched nothing) |
| 2 | The reporter receives the rejection — pending entry dropped; the local copy destroyed via the death funnel; warning logged | machine | **pass** | same run: the guest logged `host rejected acceptance.beacon … PrefabUnavailable — the pending report is dropped; the reporter is asked to remove the local copy.` and the adapter `host rejected creation acceptance.beacon … — removing the local copy.` (`a2-log-guest`); the pending table read `0` (`a2-tables-guest`) and the copy was gone from the census (`a2-census-guest`) |
| 3 | The rejection message is lost — the fallback re-reports; the host rejects again (idempotent); the state converges on the next successful rejection | machine | **pass** | a creation was made inside an armed window on the reporter (`a4-blackout-on` → spawn → `a4-blackout-off`), so the first rejection could not land: the pending entry stood at `1` (`a3-tables-guest-1`) with the copy alive (`a3-census-guest-1`). After the shared fallback window the guest logged `[EntitySpawn] re-reported 3 unacknowledged creation(s) to the host.`, the host logged a second `REJECTED … (creation 76561199526807662:3222325308)` and the guest logged `host rejected … the pending report is dropped; the reporter is asked to remove the local copy.` followed by the adapter's `removing the local copy` for that key — `a7-heal-log-guest`, `a7-heal-log-host`; the pending table drained to `0` and the census holds no `acceptance.beacon` (`a7-heal-tables-guest`, `a7-heal-census-guest`) |
| 4 | The rejection arrives after the local copy already died — no-op (key gone) | machine | **pass** | a creation inside an armed window (its rejection lost) had its copy destroyed first while the creation record was deliberately left standing: `a4-kill-beacon3` (`mode=destroy`, `healthAfter=-1`) and `a4-census-after-kill` no longer list `…:3222325307`, while `a4-tables-guest` still held the pending entry. When the re-asked rejection arrived after the shared window, the guest logged `host rejected … (creation 76561199526807662:3222325307) … the pending report is dropped` and the adapter's no-op `[DBG] rejected creation acceptance.beacon at (-2,483) has no local copy left — nothing to remove (PrefabUnavailable).` (`a7-heal-log-guest`) — no resurrection, no second removal |
| 5 | The local copy moved before the rejection arrived — found by creation key and destroyed (position-independent) | machine | **pass** | a creation inside an armed window was then moved out of its creation cell: `a5-move-beacon4` moved `…:3222325309` from `(-6.378,450.842)` to `(5.622,453.842)`, and `a5-census-after-move` still records the CREATION cell `(-7,450)` on its marker. The re-asked rejection arrived after the shared window and the adapter logged `host rejected creation acceptance.beacon at (-7,450) (creation …:3222325309) … removing the local copy` (`a7-heal-log-guest`) — the key, not the position, located it; the census then held no beacon (`a7-heal-census-guest`) |
| 6 | The host HAS the prefab — unchanged accept-first path: materialize + record + relay | machine | **pass** | the host bound the same definition (`a8-host-inject`, `a8-host-status`), the guest spawned (`a8-guest-spawn-beacon5`), the host logged `created acceptance.beacon … (creation 76561199526807662:3222325310)` with no rejection and `accepted=1` (`a8-log-host`, `a8-tables-host`); the guest's echo and the third client's relay copy both logged `created acceptance.beacon` (`a8-log-guest`, `a8-log-alt`) and the census read exactly one copy per client (`a8-census-host|guest|alt`) |
| 7 | A member joins after a rejected creation — nobody has it (converged) | machine | **pass** | after the rejected creations above, both later entries — the guest's in-place reconnect (`a9-snapshot-log-host`: `Sending world-entry snapshot group to 76561199526807662.`) and the alternate client's late join (`b1-snapshot-log-host`: `Sending world-entry snapshot group to 76561198863287957.`) — delivered only the accepted records: the censuses after each entry list exactly one `acceptance.beacon` (the accepted `…:3222325310`) and none of the rejected keys (`…:3222325306`–`…:3222325309`) (`a9-census-*`, `b1-census-*`); the host's accepted table held `0` while every rejected key existed (`a2-tables-host`, `a7-heal-tables-host`) and later held only the accepted creations (`b7-tables-host`, `accepted=3`) — no rejected key ever entered it |
| 8 | A non-reporter receives the rejection message — ignored (direction-locked host → guest; only the reporter's own key matches) | machine | **pass** | the third client was given a rejection for the guest's key as the host (`a6-alt-foreign-rejection`) and logged exactly the ignore line: `[DBG] [EntitySpawn] ignoring a rejection for acceptance.beacon at (-2,483) — creation 76561199526807662 is neither this member's token nor its pending report.` (`a6-log-alt`); its pending and accepted tables stayed `0` and its census stayed empty (`a6-tables-alt`, `a6-census-alt`) |
| 9 | A hand-built report names another member in its creation token — still answered and acted on: the pending table is the proof THIS member reported it | machine | **pass** | the guest sent one hand-built `EntitySpawnedMsg` whose token names the HOST (`76561198281246659:4000000001`, cell 10,470) through the product's own `SendEntitySpawned` (`a6-guest-forge-report`); the host logged `REJECTED acceptance.beacon at (10.5,470.5) (creation 76561198281246659:4000000001) reported by 76561199526807662` (`a6-log-host`), and the guest — whose own token is not the creation's — acted on it because its pending table held the key: `host rejected acceptance.beacon at (10.0,470.0) (creation 76561198281246659:4000000001) … the pending report is dropped` plus the adapter's no-op for the non-existent local copy (`a6-log-guest`); its pending table returned to its pre-forge count (`a6-tables-guest`) |

## Limits

- **Rows 8 and 9 are driven through the product's own entry points**, because the production host never
  sends a rejection to a non-reporter and the creating side never builds a foreign-token report itself.
  The probes call `WorldService.FireRuntimeEntityRejectedReceived` and `WorldService.SendEntitySpawned`
  (the same receive/send seams the transport feeds) against the deployed artifact, and every verdict is
  read from the product's own tables, logs and census — the batch `20261002-o` row-8 precedent for a
  state the live wire cannot produce.
- **Row 4's copy death is a direct GameObject destruction** (`runtime-entity-kill mode=destroy`) that
  deliberately leaves the creation record standing — that is the shape "the rejection arrives after the
  local copy already died"; the death-funnel variant (a copy that dies through the game and drops its own
  record) is row 2's evidence.
- **Rows 3–5 shared one fallback window.** The three creations were staged inside their own short armed
  windows and then re-asked together by the reporter's steady 60 s window; the guest's
  `re-reported 3 unacknowledged creation(s)` line names the one window, and each key's outcome is read
  from its own line and census state.
- **The host's mismatch log is the channel's Warn line** (`REJECTED … neither recorded nor relayed`) plus
  the adapter's per-entry containment line; the reason code on the wire is `PrefabUnavailable`
  (`RuntimeEntityRejectedMsg`), and the reporter's drop/no-op lines are the adapter's own.
- One session; one read per state.

## Residuals for the user

None. Every verdict above is from this run's own probes, logs and readings; no row was handed to the
user to judge.
