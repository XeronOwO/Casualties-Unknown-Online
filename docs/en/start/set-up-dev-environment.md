# Set up a development checkout

[Documentation](../../README.md) > [Start](README.md) > Set up a development checkout

**After this page** the solution builds and the test suite runs on your machine. You need the .NET
SDK and a Windows machine: every project targets `net48` because the game runs BepInEx 5 on Mono.

## Get the code, build it, test it

```bash
git clone <the repository URL>
cd CasualtiesUnknownOnline
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx
```

`dotnet test` runs both test projects: the behavioural suite, and
`tests/CasualtiesUnknownOnline.NormativeGates.Tests`, which holds the repository's own rules — the
protocol number, the reviewed API surface, the documentation pairing, the instruction-budget check. A
change that touches only documentation skips them; a change to code or tests must pass them.

## What is in the solution

| Project | What it is |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` | the mod surface — the only assembly a mod references |
| `CasualtiesUnknownOnline.Runtime` | the stable layer: protocol, session, mod loading, saves |
| `CasualtiesUnknownOnline.GameAdapter` | the only layer that knows the game's private types |
| `CasualtiesUnknownOnline.Plugin` | the BepInEx entry point that boots CUO |
| `CasualtiesUnknownOnline.ModExample` | a working example mod — the next page reads it |
| `tests/…` | the behavioural suite and the normative gates |

## Put your build into your own game

```powershell
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
```

Always pass `-GameDir` explicitly. The script deploys only the build output DLLs plus the Steam
dependency, refuses sandbox paths, and never touches BepInEx's own DLLs. Close the game first.

## Related reading

- [Your first mod](your-first-mod.md) — read a working mod end to end
- [Install and play](install-and-play.md) — what a player does with the same folder
- [Glossary](../reference/glossary.md) — runtime, adapter, kernel

[Documentation](../../README.md) > [Start](README.md) > Set up a development checkout
