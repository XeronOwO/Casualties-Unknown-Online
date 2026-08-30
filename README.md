# Casualties Unknown: Online (CUO)

A multiplayer mod framework for [*[Casualties Unknown*](https://store.steampowered.com/) (currently in Demo), built on [B[epInEx](https://github.com/BepInEx/BepInEx).

The base game ships without multiplayer. CUO adds Steam-based **Host + Guests** co-op (LAN / friends) by injecting a new multiplayer runtime and reorganizing the local-only game state into a **host-authoritative simulation with guest input/state sync** — in the spirit of Minecraft Forge, but starting with a solid multiplayer core rather than a full mod ecosystem.

## Status

**Active development — architecture evolution complete.** Phases 0–4 (feasibility, single-player entity sync, entity lifecycle, game core loop, public Mod API) are complete and runtime-verified; the typed deterministic game-state kernel migration (Phases A–E) is also complete. See [`docs/README.md`](docs/README.md) for the full documentation map, [`docs/architecture/README.md`](docs/architecture/README.md) for the active architecture, [`docs/decisions/active.md`](docs/decisions/active.md) for landed decisions, and [`docs/api/mod-api.md`](docs/api/mod-api.md) for the binding Mod API contract.

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

- [`[docs/README.md`](docs/README.md) — semantic documentation map and reading path
- [`AGENTS.md`](AGENTS.md) — project conventions and instructions for AI-assisted development
[- `docs/architecture/README.md`](docs/architecture/README.md) — active architecture and completed evolution history
[- `docs/architecture/current.md`](docs/architecture/current.md) — current typed deterministic kernel design
[- `docs/architecture/domains.md`](docs/architecture/domains.md) — domain ownership and projections
[- `docs/architecture/protocol.md`](docs/architecture/protocol.md) — four-envelope protocol and data flow
[- `docs/evidence/verification.md`](docs/evidence/verification.md) — evidence chain, gates, replay/simulation
[- `docs/operations/README.md`](docs/operations/README.md) — shared operations/tooling/deployment guidance
[- `docs/decisions/active.md`](docs/decisions/active.md) — landed binding decisions
[- `docs/decisions/index.md`](docs/decisions/index.md) — numeric decision index
[- `docs/api/mod-api.md`](docs/api/mod-api.md) — Phase 4 Mod API contract
[- `docs/backlog/README.md`](docs/backlog/README.md) — open bugs, work, decisions, future

## License

See [L[ICENSE](LICENSE). BepInEx and its dependencies are under their own licenses — verify before distributing.

## Disclaimer

*Casualties Unknown* is a third-party game with no official mod support. CUO is an unofficial community project; compatibility with future game updates is not guaranteed and is maintained through the Game Adapter layer.
