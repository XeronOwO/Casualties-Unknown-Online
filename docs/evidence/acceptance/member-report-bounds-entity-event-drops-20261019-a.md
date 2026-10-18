# Acceptance record — the entity-event drop bound on the machine

- Ticket: `member-report-bounds` — row `NetMsg.EntityEvent` (the drops half): **row 1 passes, row 2 FAILS, row 3 is
  unproven, row 4 passes**. The failure is the finding below; the ticket goes back to `todo/` with it.
- Batch: `20261019-a` — this run served one ticket, the one the session handoff opened it for: the lever the row
  owed and the four rows it named. No other ticket was in scope.
- Commit: `c909fd3d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c909fd3da42b3feadcf788b71f41d24a44e19919`, checked with `tools/verify-deploy.ps1` (exit 0;
  `verify-deploy.log`)
- Run: 2026-10-18 23:26 → 23:36 (+08:00) · Host: the physical machine · Member: the `Steam1` sandbox · No third
  client
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts` — the
  preflight reported eleven present (`prelude-preflight.txt`); the active BepInEx tree was CUO's
  (`prelude-session-environment.txt`)
- Artifacts: the `20261019-a` subdirectory of the directory named by `acceptance-artifacts-dir`

## Why this batch

The row's own acceptance paragraph says the refusal half needs a lever no recipe had — "a lever that can send a
forged `EntityEventMsg` with a hand-built drop list" — and the four rows it owes: the forged refusal and its
answer, an ordinary event's drops landing whole, a fragile-crystal break read on the machine, and an unmodified
session. That lever is this cycle's change (`trap-event-forge`, with `trap-drop-census` and
`trap-crystal-break`); the run reads the rows against the deployed artifact.

## Method

The prelude started the host and the member through `.acceptance/live/up.ps1`, created and joined one lobby and
entered one run (`up.log`: `host up`, `member up`, `inWorld` true on both). The layer this run starts on carries
no destructive trap at all (no mine, turret or effect-carrying crystal), so the fixture was staged through the
product's own admin surface: the host opened the cheat switch (`/hostrules {"AllowMemberCheatVerbs":true}`) and
granted the member (`/op <member>`); the member then summoned `landmine` entities with `/summon landmine <x>,<y>`
and the host created and replicated each one (`s3-host-rules.json`, `s4-host-op.json`, `s5-member-summon.json`,
`s10-member-summon2.json`, `s16-member-summon3.json`).

- **The census.** `trap-drop-census` ran on both clients around the member's body (`s2-*.json`, `s6-*.json`,
  `s17-*.json`): the summoned mine resolved to `anchorOwn: true`, `prefab landmine`, one table row
  (`scrapmetal` at condition 0…0.2), and the two clients agreed on the same entry. Before the fixture the same
  probe answered `listed: 0` at radius 80 and 600 on this layer (`s2-guest-census.json`,
  `s2-guest-census-wide.json`) — the layer genuinely carries no carrier, which is why the mine was summoned.
- **The real event.** A summoned mine's own update path was driven by writing the press it derives from
  (`pressed = true`, `timeSincePressed = 0.9`, the `force-mine-press` probe) so the game's own explosion runs on
  the member (the first attempt used an explosion kill instead; that path is named in the Limits). The member's
  roll folded into one event — `[ItemTrapDrop] … captured` then `[TrapDropFlush] … drops=1` — and the host
  applied it and materialized the drop (`ev-guest-trap.txt`, `ev-host-trap.txt`).
- **The forged report.** A bandage world item was created on the member (`item-drop`, `item-world-read`:
  instance `18082891413`), a second summon served as the target, and `trap-event-forge kind=MineExploded` was
  sent at it with a hand-built drop naming `bandage@0.50` and that instance (`s18-member-forge.json`). The host
  refused it by name against the target's own table and the answer destroyed the member's copy.
- **The descent.** After the three rows above were read, the host ran the game's own `skiplayer` through the
  committed `game-console` recipe (`s21-host-skiplayer.json`); both clients were back in the world 8 s later.
  Layer 1 carried 2119 building entities and thirteen crystal-prefab buildings, but the crystal probe answered
  `count: 0` `CrystalBehaviour` components again (`probe-crystals-layer1.json`), so a fragile crystal does not
  exist here either and row 3 was not readable.
- **Row 4.** The host's log was read from the run mark (23:27:05.604) to the first forged step (23:31:58.317):
  four minutes fifty-three seconds carrying the layer's own traffic and zero `[TrapDrop]` refusals; the whole
  session carries exactly one, and the member exactly one `[ItemReject]`, both the forge's (`ev-host-trap.txt`,
  `ev-guest-trap.txt`).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member's destructive event with a drop its tables cannot produce is refused by name on the host and destroyed on the reporter | machine | **pass** | the host applied the forged trigger and judged the drop against the target's own table: `[TrapDrop] <member>'s MineExploded at (20.0,470.0) claimed 1 drop(s) the 1 row(s) of the entities this host's own application destroyed or damaged cannot produce — refused by name (bandage@0.50), 0 drop(s) accepted and relayed.` — the TABLE arm, not the empty-bounds or non-carrier arm; the member was answered (`Kernel command rejected by host for item 18082891413: ItemSpawn refused (TrapDropUnaccounted).`) and the answer destroyed the local copy (`[ItemReject] refused drop 18082891413 (bandage) — destroying the local copy (TrapDropUnaccounted).`); the host's record gained no item of the claimed definition at the target (`ev-host-trap.txt`, `ev-guest-trap.txt`, `s19-host-kernel-after-mine.json`) |
| 2 | An ordinary event's drops land whole | machine | **FAIL** | the member's mine rolled its real drop and folded it into the event (`[ItemTrapDrop] local scrapmetal (id 13787924117) … captured`, `[TrapDropFlush] … drops=1`), the host applied the event (`[TrapEvent] kind=MineExploded … origin=HostApply from <member>.`, `host applied mine explosion`) and materialized the drop (`[ItemSpawn] materializing scrapmetal (id 13787924117)`), but the authoritative batch was REJECTED: `record-building-health rejected: InvariantViolation (building entity at (-20,482) has invalid health -751.9034)` and then `wire-command rejected: InvariantViolation (building entity at (-20,482) has invalid health -900.2673)` — the whole trap composite, its consumption and its item spawn included. The kernel's world rows therefore never contain `13787924117` (`s19-host-kernel-after-mine.json`), so the drop exists only as the host's presentation: a save/rejoin loses it. Root cause below |
| 3 | A fragile-crystal break, read on the machine | machine | **unproven** | this run's world carries no fragile crystal to break. Layer 0 held nine crystal-prefab buildings and layer 1 thirteen, and the probe found ZERO `CrystalBehaviour` components on either (`probe-crystals-guest.json`, `probe-crystals-layer1.json`) — the effect carrier the kind lives on is absent, so no fragile crystal exists to generate, summon or break. The run descended one layer and re-read rather than assume |
| 4 | An unmodified session meets no refusal | machine | **pass** | every `[TrapDrop]` line in the run is the one the forge provoked (23:31:58.317), and the stretch from the run mark to that step — 23:27:05.604 → 23:31:58.317, four minutes fifty-three seconds — carries none; the member's `[ItemReject]` count is exactly one, the forge's answer (`ev-host-trap.txt`, `ev-guest-trap.txt`) |

## The failure's root cause

The kernel's world-entity state validation refuses ANY building-health fact below zero:

    foreach (var health in entities.BuildingHealth)
    {
        if (float.IsNaN(health.Health) || health.Health < 0f)
        {
            throw new InvalidOperationException($"building entity at (…) has invalid health {health.Health}");

(`src/CasualtiesUnknownOnline.GameState/Domains/WorldEntities/WorldEntityDomainModule.cs`, the validation pass.)

The entity-event path feeds that invariant the GAME's raw float health:

- `EntityEventSync.ReadDestroyedTrapHealth` returns `building.health` whenever it is below the death threshold,
  and the applier's own explosion keeps damaging the trap after setting it to zero — the host read
  `-751.9034` for the mine it had just applied.
- `TrapBuildingHealthScope` captures the same raw health for every entity the host's own blast damaged; the
  destroyed ones are negative too (`-900.2673`).

`TrapStateRegistry.ReportBatch` puts both into the ONE composite that also carries the trap consumption, the
trap-state edge and every `SpawnItemCommand` for the accepted drops, so one negative value rejects all of it.
The same clamp gap exists one channel over: the explosion's building-damage report drew
`record-building-health rejected: InvariantViolation (building entity at (20,482) has invalid health -295.9813)`
and `(-20,482) … -751.9034` in the same session (`ev-host-trap.txt`) — the family's other reporter of live
health. The fix belongs at the boundary where a physical health becomes a kernel fact (one clamp to `0` for
"destroyed", or the invariant widened with a recorded reason), with the trap composite and the damage report
aligned in one change.

## Limits

- **One member, no third client.** The relayed view of the accepted/refused drops is not claimed; the host's own
  log and its kernel are the evidence.
- **The destructive carrier was summoned, not generated.** Both layers of this world carry no mine, turret or
  effect-carrying crystal, so the run created `landmine` entities through `/summon` (the product's own admin
  surface) and drove the mine's own press state. The event chain is the game's own from there, but the row does
  not say a generated mine behaves identically under every layer's trap density.
- **The first attempt at the real event used an explosion kill** (`runtime-entity-kill mode=explode`): the blast
  damage is reported to the host, whose mine copy died as a remote death and chain-reported its own
  `MineExploded`, and the member's drop was reported standalone before the pending trap existed (the drop's
  `Item.Start` preceded the chain mine's `OnDestroy`). That is why the row's fixture was re-driven through the
  mine's own press path; the chain-path ordering it exposed is named here rather than generalized into a claim.
- **Row 1's destroy is momentary.** The instance the forged drop named was also a world item the member had
  created legitimately, so the host's own creation row re-materialized it on the member a few seconds after the
  answer destroyed the local copy (`s20-member-world-items-after`). In production that id would exist only as
  the refused drop. The reading claimed is the answer path (`[ItemReject] … destroying the local copy`), not a
  permanent absence.
- **The forged trigger is applied.** The host applied the forged `MineExploded` to its own copy of the target
  (the trigger half stays unbounded by decision 265); only the riding drop was judged. The host's copy of the
  sacrificial mine is gone while the member's stands — a consequence of the forge, not of the bound.
- **Row 3 is not closable in this world.** The crystal effect carrier is absent on both layers read; the run did
  not descend further after a full layer change failed to produce one, and a fragile-crystal break needs a
  world that generated one.
