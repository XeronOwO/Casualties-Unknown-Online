# Acceptance record — Runtime-created BuildingEntity spawns have no backfill or re-report

- Ticket: `runtime-entity-spawn-backfill` — verdict: **moved to `docs/backlog/done/`** (rows 1–4, 11 and 12
  pass in this batch; rows 5–10 stand from batch `20261002-k`)
- Batch: `20261003-a` — tickets `runtime-entity-spawn-backfill`, `runtime-entity-creation-rejection`
- Commit: `3b1da7da` (artifact; the deployment this run observed) · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+3b1da7dab526a2260921e4beb58a3ee664ad0e68`
  (`tools/verify-deploy.ps1` exit 0) · The commit that carries this record is one commit later (docs-only);
  the deployed assemblies do not move with it.
- Run: 2026-10-03 00:14 → 00:25 (+08:00) · Host: physical machine · Guest: Steam1 sandbox · Third client:
  Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (11 present,
  exit 0); `session-environment -Mode status`: `active=cuo`, `game-running=false`, `swap-needed=false`.
- Artifacts: `a0-*` … `a9-*`, `b1-*` … `b9-*`, `x0-*`, `x1-*` in the directory named by
  `acceptance-artifacts-dir`; this record cites artifact ids only.

## Capability spikes (first use of the batch's own instruments)

| Spike | Result |
|---|---|
| `building-template-inject mode=bind` / `mode=status` on guest and alt | pass — `accepted=true`, then `hasTemplate=true, templates=1, failed=false, vanilla=false`; the provider logged `built runtime template for acceptance.beacon (base dropcapsule, components 0)` (`a1-inject-guest`, `a1-status-guest`, `a1-inject-alt`, `a1-status-alt`) |
| `building-template-inject` on the host, later | pass — bound last (the provider has no unbind), `hasTemplate=true, templates=1` (`a8-host-inject`, `a8-host-status`) |
| `runtime-entity-tables` ×3 (resting) | pass — `role` Host/Guest, `accepted=0, animals=0, cap=4096, pending=0` on all three (`a0-tables-*`) |
| `runtime-entity-census` ×3 (resting) | pass — `marked=0` on all three (`a0-census-*`) |
| `net-receive-blackout mode=status` / `on` / `off` | pass — `armed=false, subscribers=1` → `subscribersAfter=0, parked=true` → `subscribersAfter=1, parked=false` (`b2-host-blackout-status|on|off`, `b3-guest-blackout-status|on`, `a3-…`, `a4-…`, `a5-…`) |
| `runtime-entity-kill`, `runtime-entity-move`, `runtime-entity-rejection-forge`, `log-level` | pass — each returned `ok:true` with the expected state change or read (`a4-kill-beacon3`, `a5-move-beacon4`, `a6-guest-forge-report`, `a6-alt-foreign-rejection`, `x0-loglevel-guest|alt`) |

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest creates a runtime entity; its report is dropped — host creates its copy; third-party guests see it | machine | **pass** | the host was armed first (`b2-host-blackout-status` → `b2-host-blackout-on`: `subscribersAfter=0, parked=true`); the guest then spawned `dropcapsule` (`b2-guest-spawn-dropcapsule`, creation `76561199526807662:3222325311`) and the guest's own pending table held 1 unacknowledged report (`b2-tables-guest`); with the report genuinely lost the host census listed only its earlier creations — no `dropcapsule` — and so did the third client (`b2-census-host`, `b2-census-alt`); after `b2-host-blackout-off` the fallback re-sent (`b7-log-guest`: `[EntitySpawn] re-reported 1 unacknowledged creation(s) to the host.` and `host answered dropcapsule at (-24.8,388.6) — dropped the pending report (0 left).`), the host recorded and relayed it (`b6-census-host` and `b6-census-alt` list `…:3222325311`), and the guest's own census converged (`b9-census-guest`) |
| 2 | Host creates a runtime entity; the relay is dropped — guest converges without reconnect | machine | **pass** | the guest was armed before the host's spawn (`b4-guest-blackout-on`); the host spawned `dropcapsule` (`b4-host-spawn-dropcapsule`, creation `76561198281246659:3010082412`) and both the host and the third client held it (`b4-census-host`, `b4-census-alt`) while the guest did not (`b4-census-guest`); no reconnect was performed; the host's absolute table arrived on the existing member and the guest logged `applied host runtime-entity snapshot (3 entries, 0 animal acknowledgements).` (`b9-guest-heal-log`, 00:23:32) with the host's creation present exactly once (`b9-census-guest`) |
| 3 | Late joiner enters the world — all runtime entities present exactly once | machine | **pass** | the alternate client left the world and the lobby (`b1-alt-leave`, `b1-alt-state-out`: `role=None, inWorld=false`), then joined the running lobby (`b1-alt-rejoin`); the host logged `Sending world-entry snapshot group to 76561198863287957.` (`b1-snapshot-log-host`) and the member logged `World join received — starting a run to follow.` plus `applied host runtime-entity snapshot (1 entries, 0 animal acknowledgements).` (`b1-snapshot-log-alt`); the census then read exactly one `acceptance.beacon` (`…:3222325310`) per client, same key and position (`b1-census-host|guest|alt`) |
| 4 | Reconnect while in world — same; no duplicates | machine | **pass** | the guest left the world in place (`a9-guest-leave`, `a9-guest-state-out`), left the lobby and re-joined the same lobby id (`a9-guest-rejoin2`); the host logged `Sending world-entry snapshot group to 76561199526807662.` (`a9-snapshot-log-host`), the guest logged `World join received — starting a run to follow.`, re-created the record (`created acceptance.beacon at (-20.73, 385.17) (creation 76561199526807662:3222325310)`) and applied `host runtime-entity snapshot (1 entries, 0 animal acknowledgements)` (`a9-snapshot-log-guest`); the census read exactly one copy of each creation on all three with no duplicate (`a9-census-host|guest|alt`), the guest's pending table drained (`a9-tables-guest`) and the host's accepted table stayed at its one record (`a9-tables-host`) |
| 11 | The host cannot materialize a reported creation — rejected, reporter answered, local copy destroyed | machine | **pass** | the host had no template while the guest did; the guest spawned `acceptance.beacon` (`a2-guest-spawn-beacon1`, creation `76561199526807662:3222325306`); the host logged `cannot create acceptance.beacon … Utils.Create threw (missing prefab or template).` then `REJECTED acceptance.beacon … neither recorded nor relayed.` (`a2-log-host`), its accepted table stayed 0 (`a2-tables-host`) and the third client never materialized it (`a2-census-alt`); the reporter dropped the pending report and the adapter removed the local copy (`a2-log-guest`: `host rejected … the pending report is dropped` then `removing the local copy`), ending at `pending=0` (`a2-tables-guest`) and `marked=0` (`a2-census-guest`) |
| 12 | A MOD-registered template (building or animal) — materialized, never rejected by a `Resources.Load` pre-check | machine | **pass** | the host bound the same `ModBuildingDefinition` the members carried (`a8-host-inject`, `a8-host-status`: `hasTemplate=true, templates=1`) and the provider logged `built runtime template for acceptance.beacon (base dropcapsule, components 0)`; the guest then spawned it (`a8-guest-spawn-beacon5`) and the host materialized and recorded it — no `cannot create`/`REJECTED` line — `created acceptance.beacon at (-20.73, 385.17) (creation 76561199526807662:3222325310)` (`a8-log-host`), `accepted=1` (`a8-tables-host`), the guest's relay echo answered it (`a8-log-guest`: `created acceptance.beacon` + `host answered … dropped the pending report`) and the third client materialized the relay (`a8-log-alt`: `created acceptance.beacon`); the census read exactly one copy on all three (`a8-census-host|guest|alt`) |

## Limits

- **The template was staged through the content provider's own public bind seam, not mod discovery.**
  `building-template-inject` calls `GameAdapterBuildingContentProvider.TryBind` with the same
  `ModBuildingDefinition` payload a shared-content mod registers in `Bind`, and the provider builds the
  template in its own `Update`. It therefore does not re-prove mod discovery, the handshake or the
  content binder's shared-mode filter; those stay Mod-API evidence. Rows 11 and 12 judge the
  materialization/rejection paths, which is what they are about.
- **Rows 5–10 are not re-run here.** Their batch `20261002-k` verdicts stand (creation payload,
  destruction before the re-report, layer reset, duplicate delivery, same-cell siblings, animal
  acknowledgement); this batch judges only the six that were `unproven` there.
- **Row 3's staged shape.** The "late joiner" is the alternate client leaving the world and the lobby
  and joining the running lobby again; the entry edge it takes is the same one a member that never
  entered would take (batch `20261002-o` recorded the same shape).
- **Row 4's re-entry needed the lobby leave too.** `leave-world` alone keeps the client a lobby member
  and `join-lobby` is then a no-op; the staged path was `leave-world` → `home.leave` →
  `join-lobby <same id>`, after which the host's `Sending world-entry snapshot group to <member>`
  line is the entry evidence. The verdict read is taken after that group applied, never on the
  `join-lobby` return (the `20261002-o` row-6 lesson).
- **Row 2's heal is the periodic absolute table.** The member converged on the host's existing
  in-session repair/absolute snapshot with no reconnect; the record reads `applied host runtime-entity
  snapshot (3 entries, …)` rather than a timing claim.
- **One session; one read per state.** Every census/tables pair brackets one state; the blackout
  windows were single-stepped (status → arm → the one write → disarm → re-read) and the log levels
  were restored to Information before the close.

## Residuals for the user

None. Every verdict above is from this run's own probes, logs and readings; no row was handed to the
user to judge.
