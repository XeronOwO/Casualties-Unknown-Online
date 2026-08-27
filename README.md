# Casualties Unknown: Online (CUO)

A multiplayer mod framework for [*Casualties Unknown*](https://store.steampowered.com/) (currently in Demo), built on [BepInEx](https://github.com/BepInEx/BepInEx).

The base game ships without multiplayer. CUO adds Steam-based **Host + Guests** co-op (LAN / friends) by injecting a new multiplayer runtime and reorganizing the local-only game state into a **host-authoritative simulation with guest input/state sync** — in the spirit of Minecraft Forge, but starting with a solid multiplayer core rather than a full mod ecosystem.

## Status

**Active development — Phase 3 native game-content follow-through.** Phases 0–3 (feasibility, single-player entity sync, entity lifecycle, game core loop) are complete and runtime-verified; the remaining base-game coverage is now the priority (see [`docs/backlog.md`](docs/backlog.md)). Phase 4 (Mod API) has landed the core skeleton (discovery / lifecycle / manifest / mod messages / handshake consistency) plus the permission model, host commands, dependency ordering, SemVer versions, mod-state saves, local mod UI, and content registration; the remaining Mod API surface — custom entities — is MEDIUM priority and resumes after the native game content is fully covered. See [`docs/architecture.md`](docs/architecture.md) for the full design and phases, [`docs/tech-decisions.md`](docs/tech-decisions.md) for the landed decisions, and [`docs/mod-api.md`](docs/mod-api.md) for the binding Mod API contract.

## Architecture in Brief

```
Mods → Mod Framework API → Multiplayer Runtime → Game Adapter → BepInEx / Unity / Steam
```

- **Stable CUO Runtime**: network protocol, host/guest state machine, mod loading, serialization, tick/snapshot, logging, version negotiation.
- **Replaceable Game Adapter**: the only layer that knows the game's private types; one adapter per game build, with startup capability detection and safe degradation when a game update breaks compatibility.
- **Accept-first sync arbitration**: the host trusts each guest's reports first (adopt and relay, never blocking the player's action) and corrects only on obvious conflicts such as races; strict validation and anti-cheat are deliberately low priority until the feature set is complete.

## Build

Requires .NET SDK (see [`AGENTS.md`](AGENTS.md)).

```bash
dotnet build CasualtiesUnknownOnline.slnx
```

All projects target `net48` (BepInEx 5 + the game's Mono runtime). Deployment into the game's `BepInEx/plugins/CasualtiesUnknownOnline/` folder is handled by `deploy.ps1`.

## Documentation

- [`AGENTS.md`](AGENTS.md) — project conventions and instructions for AI-assisted development
- [`docs/architecture.md`](docs/architecture.md) — architecture blueprint, technical specs, pitfalls, and development phases
- [`docs/architecture-evolution/README.md`](docs/architecture-evolution/README.md) — planned architecture iteration toward the typed deterministic game-state kernel (phase plans, status, session handoff)
- [`docs/tech-decisions.md`](docs/tech-decisions.md) — landed binding decisions
- [`docs/mod-api.md`](docs/mod-api.md) — Phase 4 Mod API contract

## License

See [LICENSE](LICENSE). BepInEx and its dependencies are under their own licenses — verify before distributing.

## Disclaimer

*Casualties Unknown* is a third-party game with no official mod support. CUO is an unofficial community project; compatibility with future game updates is not guaranteed and is maintained through the Game Adapter layer.
