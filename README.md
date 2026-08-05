# Casualties Unknown: Online (CUO)

A multiplayer mod framework for [*Casualties Unknown*](https://store.steampowered.com/) (currently in Demo), built on [BepInEx](https://github.com/BepInEx/BepInEx).

The base game ships without multiplayer. CUO adds Steam-based **Host + Guests** co-op (LAN / friends) by injecting a new multiplayer runtime and reorganizing the local-only game state into a **host-authoritative simulation with guest input/state sync** — in the spirit of Minecraft Forge, but starting with a solid multiplayer core rather than a full mod ecosystem.

## Status

**Early development — Phase 0 (feasibility).** The repository currently contains the BepInEx plugin skeleton; no networking code yet. See [`docs/architecture.md`](docs/architecture.md) for the full design.

## Architecture in Brief

```
Mods → Mod Framework API → Multiplayer Runtime → Game Adapter → BepInEx / Unity / Steam
```

- **Stable CUO Core**: network protocol, host/guest state machine, mod loading, serialization, tick/snapshot, logging, version negotiation.
- **Replaceable Game Adapter**: the only layer that knows the game's private types; one adapter per game build, with startup capability detection and safe degradation when a game update breaks compatibility.

## Build

Requires .NET SDK (see [`CLAUDE.md`](CLAUDE.md)).

```bash
dotnet build src/CasualtiesUnknownOnline/CasualtiesUnknownOnline.csproj
```

The plugin targets `net35` (BepInEx 5). The built DLL is deployed into the game's `BepInEx/plugins/` folder.

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — project conventions and instructions for AI-assisted development
- [`docs/architecture.md`](docs/architecture.md) — architecture blueprint, technical specs, pitfalls, and development phases

## License

See [LICENSE](LICENSE). BepInEx and its dependencies are under their own licenses — verify before distributing.

## Disclaimer

*Casualties Unknown* is a third-party game with no official mod support. CUO is an unofficial community project; compatibility with future game updates is not guaranteed and is maintained through the Game Adapter layer.
