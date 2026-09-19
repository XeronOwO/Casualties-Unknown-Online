# Direct player-interaction line-of-sight / visibility gate (no protocol bump)

Date: 2026-08-27
Scope: add the shared direct-visibility gate after the direct player-interaction
set became stable, so take/carry/piggyback/heal/use/push/recruit and the remote
backpack view cannot be performed through walls.

## What landed

- **Runtime seam** — `IPlayerInteractionVisibility` with a single
  `HasLineOfSight(observer, target)` method; a default allow-all implementation
  keeps the base composition/test root usable.
- **Game-backed oracle** — `GameAdapter.PlayerInteractionVisibility` uses the
  local body or the 20 Hz entity positions and a Ground-only
  `Physics2D.Linecast` (the same layer the vanilla pickup check uses). Missing
  position data logs and allows, never blocks on missing sync.
- **Authoritative checks** — the host request handlers for take, carry start,
  heal, consumable use and push refuse a confirmed wall before mutating
  snapshots/transfer tables. The native remote-backpack open and trader-recruit
  host path use the same oracle. **[SUPERSEDED 2026-09-19 — stage 1 of
  `todo/remote-interaction-local-gating.md` moved every one of these judgments to the
  actor's own request-formation seam; the host handlers no longer call the oracle at all
  (`interaction-gate-authority-selfcheck.md`).]**
- **UI presentation** — `OnlineUiMemberRow.CanSee` comes from the same oracle.
  Without LOS the Players page hides direct-action buttons, take lists, native
  open-backpack, and the inventory text fallback; local rows stay visible.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Runtime interface | `IPlayerInteractionVisibility` + default allow-all | `Runtime/Session/PlayerInteraction` |
| DI wiring | default registered in `CuoBootstrap`; plugin replaces with `GameAdapterImpl` | `CuoBootstrap.cs`, `PluginDependencyRegistrar.cs` |
| Adapter oracle | positions + Ground linecast, missing evidence allowed | `GameAdapter/PlayerInteractionVisibility.cs`, `GameAdapter.cs` |
| Take/carry/heal/use/push | **[superseded 2026-09-19]** the ACTOR's `Send*Request` calls the oracle; the host handlers no longer do | `PlayerInteractionService` subservices |
| Remote backpack view | coordinator refuses open without LOS | `RemoteBackpackCoordinator.cs` |
| Trader recruit | actor's send path refuses without LOS (host re-check deleted 2026-09-19) | `TraderRecruitCoordinator.cs` |
| UI projection | `CanSee` hides action/inventory surfaces | `OnlineUiMemberProjection.cs`, `OnlineUiMemberRow.cs`, drawers |
| Wire | **No protocol/wire change** | no NetMsg/ProtocolVersion touched |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings/errors.
- `dotnet test CasualtiesUnknownOnline.slnx` — full suite green.
- New tests: take/carry/heal/use/push blocked-by-visibility leave authoritative
  state untouched; projection blocks action buttons and inventory surfaces.
- Static evidence: all new code crosses only existing runtime/game boundaries;
  no matrix/protocol/event row touched.
- Manual acceptance: not requested for the developer cycle; L0 + static
  evidence, no manual acceptance.

## Accepted limitations

- The gate is a confirmed-wall blocker; it does not validate angle, range,
  collision boxes or line-of-sight through transparent/one-way objects.
- Guest-side request buttons are hidden when the local oracle says blocked, and
  since 2026-09-19 the actor's own client is the only judge of its reach: the host
  does not re-judge a remote actor, so a client that lies about its own line of
  sight is not caught (anti-cheat is a non-goal of the ticket), and a frame crafted
  around the client gate reaches the host ungated.
- The `Ground` linecast is the same vanilla gameplay query used by pickup;
  future game-update changes to layer names would surface in the patch/contract
  gates because the implementation lives in the GameAdapter.
