# CUO Exploration — 2026-08-23

Scope: a parallel sub-agent sweep for (a) original-game mechanics that may still
lack multiplayer support, (b) valuable KrokMP mechanics worth adopting in a
CUO-native way, and (c) architecture/quality debt found during the sweep.

This file is an exploration record, not a binding design. Landed decisions
belong in `docs/tech-decisions.md`; actionable open items are summarized in
`docs/backlog.md`.

## 1. Original game mechanics — likely gaps

### 1.1 RadiationLine world state — CLOSED (landed 2026-08-23)

- Original: `RadiationLine` advances `timeGone`, applies `radiationSickness`
  and eye-scare/irradiation presentation to bodies above the line, and is
  activated/deactivated by world generation / layer-time logic.
- **Landed**: a new host→guest `RadiationLineStateMsg` (NetMsg 106,
  ProtocolVersion 33) carries the host-authoritative `active` + `timeGone`
  state. The host publishes it while the line is active (idempotent 5 Hz
  self-heal) and stores the current state for the world-entry/reconnect
  fan-out; guests apply the absolute state, keep running their local
  per-frame line presentation/body effects between resends, and their
  independent `layerTimeSpent` activation is suppressed in
  `WorldGenerationUpdatePatch`. See `docs/selfchecks/radiation-line-state-sync-selfcheck.md`
  and `docs/tech-decisions.md` #55.

### 1.2 CrystalTeleport matrix coverage — CLOSED (landed 2026-08-23)

- Original: `CrystalBehaviour.possibleEffects` includes `CrystalTeleport`; it
  teleports the local player and changes consciousness/shock/velocity with a
  one-shot presentation effect.
- **Landed**: `CrystalTeleportTriggered` (EntityEventKind 33, ProtocolVersion
  34) is a repeatable entity event reported when the touching body actually
  moves (dynamic prefix/postfix on the internal `CrystalTeleport.Touched`).
  The host executor and guest replay both run the trigger-side 2D
  `observerlaugh` + `FlashBrief`; the body position/consciousness/shock/velocity
  continue to ride the 20 Hz player stream. No late-joiner replay (repeatable,
  no latch). See `docs/selfchecks/crystal-teleport-sync-selfcheck.md` and
  `docs/tech-decisions.md` #56.

### 1.3 Owner-local body auto-event presentation — CLOSED (landed 2026-08-23)

- Vomiter, SelfHarmer, PantSound, MoodChangeSounds, and similar body-driven
  one-shot sounds/visuals are not explicitly part of the CUO clone presentation
  contract.
- **Landed**: `RemoteBodyFactory.CreateRemoteBody` now disables every
  owner-local body auto-event component on render clones (`Vomiter`,
  `SelfHarmer`, `PantSound`, `MoodChangeSounds`, `SleepingBagUse`). These
  components' `Update` methods are not skipped by the `Body.Update`/`Limb.Update`
  render-proxy patches, and `MoodChangeSounds`/`SleepingBagUse` read
  `PlayerCamera.main.body` (the local player), so leaving them enabled could
  double local mood sounds or even destroy a clone from the local player's
  sleeping-bag state. The effects remain owner-local by design; a future remote
  presentation would need a dedicated event path. See
  `docs/selfchecks/owner-local-body-auto-events-selfcheck.md` and
  `docs/tech-decisions.md` #57.

## 2. KrokMP mechanics worth considering

KrokMP's network transport (LiteNetLib raw UDP) and its "everyone simulates
everything" internals are explicitly NOT a model to copy. The following are
mechanism-level candidates that fit CUO's host-authoritative world / accept-first
sync model.

### 2.1 Trader Recruit — revive a dead player at a trader — HIGH

> **Status 2026-08-23: first slice CLOSED** — host-authoritative
> `TraderRecruitRequest`/`TraderRecruitResult` (NetMsg 107/108,
> ProtocolVersion 35) with in-place health revive; the broader revive lifecycle
> is closed (§2.2). **Random trader-stock bonus items are also CLOSED**: a
> successful recruit grants the revived player 1–3 distinct items from the
> host trader's current stock via `TraderRecruitResult.Items`
> (ProtocolVersion 37). See
> `docs/selfchecks/trader-recruit-selfcheck.md` and
> `docs/selfchecks/trader-recruit-gift-items-selfcheck.md`.

- KrokMP: a trader can be recruited when its health and reputation gates pass;
  the server picks a dead player to respawn, gives 1–3 random trader items, and
  destroys the trader. It has its own `SERVER_TraderRecruit` message and UI
  button.
- CUO: the trade domain is already host-authoritative and well covered
  (`TraderState`, `TraderAction`, `TradeExecutor`), but `TraderActionKind` has no
  Recruit action and there is no survive/revive flow.
- Value: adds a cooperative "death is not necessarily run-ending" mechanic and
  reuses the existing trader domain.
- Complexity: medium-high; needs a host-side dead-player roster, an authoritative
  revive/apply path, and idempotency/concurrency handling.

### 2.2 Revive/respawn rules — CLOSED (landed 2026-08-23)

- KrokMP rule bits: `Permadeath`, `ReviveOnNextLevel`, `ReviveFromTrader`,
  `RespawnKeepInventory`, `RespawnKeepSkills`; death handling is integrated with
  save/level transitions.
- CUO: no respawn semantics at exploration time; `SessionStatePump` documented
  death as the end of the run. Character data persistence covered reconnect,
  not revival.
- **Landed**: `RespawnOptions` (BepInEx `[Respawn]` rules, hot-reloadable),
  `RespawnPolicy` (pure gates + respawn shaping), `RespawnCoordinator`
  (host generation-finished edge), full-restore delivery for guests and local
  host, and targeted `WorldJoinTo` re-entry for dead players that already left
  the world. No protocol change. See `docs/selfchecks/respawn-rules-selfcheck.md`
  and `docs/tech-decisions.md` #60.
- Value: core to extended co-op sessions.
- Complexity: high; the lifecycle is distinct from new-run reset (new runs
  still clear saved characters).

### 2.3 Radiation line / straggler pressure — CLOSED (landed 2026-08-23)

- KrokMP: starts the radiation line when enough players have reached the layer
  bottom and stragglers remain; synchronizes `radlineactive` / `radlinestate` in
  world state; slows progress when players are unconscious; applies body damage
  to players caught above the line.
- CUO: has per-body `RadiationSickness`, but no line/straggler rule.
- Value: directly improves co-op pacing and is the natural multiplayer extension
  of the original game's radiation line.
- Complexity: medium-high; needs host-side per-player layer progress and a
  world-state sync path, while applying local body effects client-side.
- **Landed**: the world-state half landed earlier (NetMsg 106, #55); the
  remaining host-side straggler detection/pressure rule now landed as
  `RadiationStragglerPolicy` + `RadiationLineSync` host activation. See
  `docs/selfchecks/radiation-straggler-pressure-selfcheck.md` and
  `docs/tech-decisions.md` #58.

### 2.4 Host rules / configurable game rules — CLOSED (first slice, 2026-08-23)

> **Status 2026-08-23: CLOSED (first slice)** — a small independent
> host-rules service landed (`HostRulesOptions` + `HostRulesService`/`IHostRules`
> + `HostRulesPolicy`), composing PVP, auto-continue, late join, save-inventory
> and revive-related flags. The first wired behavior is `AllowLateJoin`: a
> brand-new member is rejected when the host is already in-world and late join
> is disabled. PVP remains reserved until the damage domain exists; auto-continue
> is surfaced but not wired yet. No wire/protocol change. See
> `docs/selfchecks/host-rules-selfcheck.md` and `docs/tech-decisions.md` #74.

- KrokMP has a broad rules struct (PVP, auto-continue, late-join, save
  inventory, teams, etc.) plus rule sync and lobby metadata.
- CUO now has a minimal host-rules service rather than a rules message/UI; this
  is the intended first slice.
- Recommendation followed: do NOT copy a 60-field struct. Start with a minimal
  host rules service for the highest-value flags as an independent domain.

### 2.5 Text chat — MEDIUM-HIGH

> **Status 2026-08-23: CLOSED** — a simple host-relayed text-chat line landed
> as `ChatMsg` (NetMsg 109, ProtocolVersion 36), with a bounded Runtime
> recent-buffer and a bottom-right IMGUI chat panel. The host validates sender
> identity/text and relays to the other members. Voice remains future work. See
> `docs/selfchecks/chat-selfcheck.md` and `docs/tech-decisions.md` #61.

- KrokMP ships a full chat box with speech-impaired/hearing-loss distortion and
  server announcements.
- CUO only syncs in-world Talker bubbles via `SpeechMsg`.
- A simple `ChatMsg`/UI layer is likely the right first communication feature;
  voice is much larger and should wait.

### 2.6 PVP — MEDIUM-HIGH but complex

- KrokMP has a player-vs-player attack pipeline, hit checks, knockback, mood
  debuffs, team rules.
- CUO has no player-to-player damage domain.
- Recommendation: defer behind PvE and a host-rules foundation; requires careful
  use of the accept-first model without strict anti-cheat.

### 2.7 Other lower-priority candidates

- Voice chat (Opus, push-to-talk/range attenuation) — high complexity, medium
  value after text chat.
- Admin commands / kick / ban / vote-kick — medium value for friend sessions;
  more relevant for public/dedicated servers later. Kick and ban have both
  landed as closed slices (see `docs/selfchecks/host-kick-selfcheck.md` and
  `docs/selfchecks/host-ban-selfcheck.md`); vote-kick remains open.
- Co-op keybinds, push/piggyback, status icons, richer player list — functional
  polish, low-medium.
- Protocol quantization/compression — explicitly measurement-first; CUO already
  has this in backlog as "do not optimize before data".

## 3. Architecture / quality audit

### 3.1 Partial-aware architecture gate — CLOSED (2026-08-23)

> **Status 2026-08-23: CLOSED (gate); debt flattening remains a follow-up.** —
> `tools/check-architecture.ps1` now aggregates by complete top-level type
> across partial files. Unrecorded debt or growth beyond the recorded debt
> ledger fails; `-Strict` refuses all recorded debt. The first real split
> landed: `WorldBuildingEntitySync` extracted from `WorldEventSync`. Remaining
> recorded aggregate debt is tracked in `docs/architecture-debt.json` and
> listed in `docs/backlog.md` as the follow-up "large logical class debt
> flattening" item. See `docs/selfchecks/partial-aware-gate-selfcheck.md` and
> `docs/tech-decisions.md` #65.

- The old gate checked per-file line counts; partial classes could hide a
  logical class far above 600 lines.
- Observed aggregate sizes: `ModService` 1590, `GameAdapter` 1397,
  `ItemService` 928, `WorldService` 899, `EnemySyncCoordinator` 750,
  `PlayerInteractionService` 716, `ItemApplication` 630.
- Original proposal: aggregate by top-level type across partials and enforce
  real responsibility splits, not just physical file movement.

### 3.2 NetMsg direction registry — CLOSED (2026-08-23)

> **Status 2026-08-23: CLOSED** — `PacketReceiver.IsValidDirection` is no
> longer a fail-open switch. Every `[PacketHandler]` carries an explicit
> `NetMessageDirection`; `NetMessageRegistry` is built once from all Runtime
> handlers (direction + payload type) and is read by the receiver (unknown ids
> dropped), sender (unknown sends refused) and dispatcher (startup consistency).
> Reliability is intentionally not a registry boolean because several messages
> are legitimately sent both reliably and unreliably by path. See
> `docs/selfchecks/netmsg-registry-selfcheck.md` and
> `docs/tech-decisions.md` #63.

- The old manually maintained switch defaulted unknown/new message types to
  valid.
- The original proposal was a single `NetMessageRegistry` (or expanded
  `PacketHandlerAttribute`) carrying direction/reliability/payload type, read by
  both dispatcher and receiver, with fail-closed behavior for unregistered
  messages.

### 3.3 `HandlerContext` god-object — CLOSED (2026-08-23)

> **Status 2026-08-23: CLOSED** — `HandlerContext` no longer owns
> `SendWorldStateToMember`; that flow lives in `WorldEntryFanout` (see §3.4).
> The remaining broad per-message control plane is now also closed: every
> packet handler receives only the narrow capability interface it declares
> (`PacketHandlerBase<TPacket, TContext>`), and `HandlerContext` remains the
> single internal composition root at the dispatch seam.

- `HandlerContext` used to inject many control-plane services into every
  handler and also owned world-entry state fan-out.
- Proposed: narrow handler dependencies to per-domain interfaces and move
  world-entry fan-out into a dedicated service.
- **Moved**: `WorldEntryFanout` now owns the world-entry snapshot group +
  completion marker; `SceneStateHandler` / `HandshakeHandler` depend on it
  directly.
- **Landed**: `Session/HandlerContexts/` capability interfaces +
  `PacketHandlerBase<TPacket, TContext>`; business handler signatures no
  longer reference `HandlerContext`; `NetMessageRegistry` payload derivation
  updated for the two generic arguments. See
  `docs/selfchecks/handler-context-narrowing-selfcheck.md` and
  `docs/tech-decisions.md` #73.

### 3.4 World-entry snapshot completion semantics — CLOSED (2026-08-23)

> **Status 2026-08-23: CLOSED** — the world-entry fan-out now sends an
> explicit `WorldSnapshotComplete` (NetMsg 110, ProtocolVersion 38) after the
> full snapshot group. The receiver raises `WorldSnapshotCompleteReceived`, so
> a full authoritative backfill is distinguishable from a partial best-effort
> state. See `docs/selfchecks/world-entry-completion-selfcheck.md` and
> `docs/tech-decisions.md` #64.

- Reconnect/late-join used to send several independent snapshot messages with
  no explicit "complete world-entry snapshot set" signal.
- Original proposal: a completion marker (or batched snapshot message) so
  receivers can distinguish a full world state from partial-best-effort state.

### 3.5 GameAdapter testability / concrete service dependencies — MEDIUM

- Several adapter domain objects depend on concrete `SessionService` and reach
  into Unity statics (`FindObjectsOfType`, `Resources.Load`, `Utils.Create`,
  private reflection).
- Proposed: narrow interfaces for world/object lookups, spawn factories, and
  session identity; keep pure arbitration in pure-machine classes and make the
  Unity seam injectable for L0 simulation.

### 3.6 Log levels on high-frequency paths — CLOSED (landed 2026-08-23)

- 1 Hz character-data and periodic sync logs are emitted at Information.
- Proposed: drop periodic paths to Debug/Verbose; keep join/leave/restore/refusal
  and failures at Information/Warn/Error, and rely on `[NetworkTraffic]` /
  `[NetworkHealth]` for metrics.
- **Landed**: the periodic paths moved to Debug/Verbose — 1 Hz character
  snapshot/relay, fluid region stream send/apply, and the 5 s trader fallback
  snapshot; one-shot/error paths remain at their proper levels. See
  `docs/selfchecks/log-level-cleanup-selfcheck.md`.

### 3.7 Already-good areas (no change needed)

- Main-thread marshaling is correctly confined to `ICuoService.Update` paths.
- Packet routing uses the handler attribute + generic base pattern, not a giant
  switch.
- Control/data plane separation is in place.
- Sync-chain ownership (`CallContext`, `OperationTrace`, verified commit) is
  present as the structural backbone.

## 4. Recommended ordering

1. Trader Recruit + minimal revive semantics (highest gameplay value, reuses
   trade domain).
2. Radiation line world-state sync + straggler rule (high value, analogous to
   earthquake/world-state patterns).
3. Pre-final-acceptance architecture cheese: NetMsg direction fail-closed,
   partial-aware gate, log-level cleanup.
4. Minimal host rules service and text chat once the above are stable.
5. PVP and voice only after rules/co-op foundations exist.
6. Protocol optimization remains measurement-first as already recorded.
