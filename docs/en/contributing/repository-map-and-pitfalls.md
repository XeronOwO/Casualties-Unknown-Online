# Repository map and pitfalls

[Documentation](../README.md) > [Contributing](README.md) > Repository map and pitfalls

---

**After this page** you can say which project a new file belongs to, what it may reference, and which
traps this repository is known for. Read [Build, test and deploy](build-and-test.md) first if you have
not built the solution yet.

## The projects

```text
src/CasualtiesUnknownOnline.Abstractions/      # public API; the ONLY package mods may reference
src/CasualtiesUnknownOnline.GameState/         # the deterministic kernel; references no other project
src/CasualtiesUnknownOnline.Protocol/          # wire DTOs and codecs only; no kernel/runtime reference
src/CasualtiesUnknownOnline.Application/       # admission seam + kernel replication
src/CasualtiesUnknownOnline.Runtime/           # DI, logging, BepInEx, Steam, session; never game assemblies
src/CasualtiesUnknownOnline.GameAdapter/       # the ONLY project referencing game assemblies; HarmonyX
src/CasualtiesUnknownOnline.Plugin/            # BepInEx 5 entry point; thin lifecycle driver
src/CasualtiesUnknownOnline.ModExample/        # the sample mod; references Abstractions only
src/CasualtiesUnknownOnline.PinyinSearch/      # satellite mod, game-binding half
src/CasualtiesUnknownOnline.PinyinSearch.Core/ # the same mod's game-free half
tools/CasualtiesUnknownOnline.ContractTool/    # contract-row generator; metadata-only, references no src/ project
CasualtiesUnknownOnline.slnx                   # solution
references/                                    # game assemblies, gitignored, populated on demand
reversing/                                     # reverse-engineering workspace, gitignored, never edited
artifacts/                                     # gate and tool output, gitignored
```

## The dependency direction is declared and enforced

`GameState`, `Protocol` and `Abstractions` are the bottom and reference no other project.
`Application` is the only path up from the kernel and may reference `GameState` and `Protocol` only;
`Runtime` reaches the kernel through `Application` (Runtime → Application → GameState) and declares no
direct `GameState` reference, so a session-level decision such as command eligibility has one owner
instead of one per entry point. `GameAdapter` and `Plugin` sit above `Runtime` and never reach
`GameState` directly. Tests and tools are consumers and are not constrained:
`tools/CasualtiesUnknownOnline.ContractTool` reads a game build as metadata and produces the contract
rows a gate compares against the adapter's own declaration, and no `src/` project references it or is
referenced by it.

The declared table is `ProjectDirectionPolicy.AllowedReferences` and the consumer list is
`ProjectDirectionPolicy.ConsumerProjects`, both in
`tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionPolicy.cs`. Adding a reference —
or a project — means declaring it there in the same change; `ProjectDirectionGateTests` fails
otherwise, and its synthetic cases pin that an upward reference, a Runtime that skips the layer, an
undeclared project and a consumer reaching down are all refused.

A satellite mod lives in this repository beside the framework and keeps the same line: only its
game-binding half references the game assemblies, and its game-free half is what the test project
references directly. Before deciding whether a system belongs in the plug-in or in its own mod, apply
the four-layer rule in [advanced-modification-policy.md](../../api/advanced-modification-policy.md)
§1.2 ("which systems live where").

## Where a document goes

- `docs/en/` and `docs/zh/` — the paired human documentation, path for path identical
  ([Writing documentation](documentation-standard.md)).
- `docs/standard/` — the terminology and pair-alignment registries the rules depend on.
- `docs/contracts/` — the machine baselines and tables the gates and tools read
  ([`abstractions-api-baseline.txt`, the two feature matrices](../../contracts/README.md)); the JSON
  baselines under `docs/evidence/` still sit beside their subject.
- `docs/api/` — legacy pages whose conclusions are moving into the blocks; deleting them is the last
  step of the migration.
- `docs/backlog/`, `docs/evidence/`, `docs/decisions/` and the other process records — English only,
  outside the human navigation; their conclusions belong in the pages above.

## Known pitfalls

- A number in a committed document (a count, a line count, a suite total) must be reproducible from
  the tree it describes or carry its source; a figure measured from an uncommitted intermediate state
  is not a fact about the repository.
- Steam P2P is not plain LAN UDP; never mix the two modes.
- Synchronizing Transforms fails on physics, parenting, animation, navigation, rigidbodies and scene
  loads — synchronize game-semantic state instead.
- Hardcoded offsets and private fields break on every game update; scan for the feature instead.
- Harmony patch state leakage: a Prefix that clears an instance field must have its Postfix restore
  it, and a report is sent only after a verified write.
- A Steam receive batch is all-or-nothing: catch per message and release in `finally`.
- Lobby identity must follow the actual lobby, not process history; late Steam initialisation must
  refresh the downstream snapshots that captured a `SteamId` of 0.
- Undefined failure modes are not acceptable: define disconnect, dropout and version-mismatch
  behaviour instead of leaving them to chance.
- `System.Memory` hijacks `Reverse()` on arrays; use a reverse-index loop or `Enumerable.Reverse`.
- Moving a type between projects or namespaces needs the import at every consumer: a file inside the
  old namespace resolved the type by simple name and so carries no `using` for the new one. When the
  move surfaces as `CS0246`/`CS0738` for the moved types while same-assembly types still resolve, the
  cause is that missing `using` — not stale artifacts, the compiler server or type visibility.
- In this repository a plain public constructor in `Abstractions` fails that project's build with
  `IDE0290` (`.editorconfig` sets it to error alongside `EnforceCodeStyleInBuild`), and a project that
  fails to build leaves its consumers compiling against the last good DLL. Use a primary constructor
  and never silence it with a `#pragma` or an `.editorconfig` override.
- `CasualtiesUnknownOnline.Application` is a namespace, and a namespace member beats a `using` alias,
  so inside any `CasualtiesUnknownOnline.*` namespace a bare `Application` binds to it instead of
  `UnityEngine.Application` — surfacing as `CS0234: … does not contain 'persistentDataPath'`. Alias
  the Unity type (`using UnityApplication = UnityEngine.Application;`) at those call sites.
- Each `net48` project needs its own `System.Runtime.CompilerServices.IsExternalInit` shim for
  `init`-only accessors and positional records; without it a project using a `record` fails with
  `CS0518` even though a sibling project uses the same language feature.
- In the test host, a method whose body touches `Component.transform`, `GetComponent`, `Physics2D` or
  `AddComponent` raises `SecurityException`; a pure field read is testable. Keep game-object access
  behind the adapter seam you are testing through, not in the test method itself.
- The test project does not reference the Game Adapter, so a defect there can hide behind a green test
  run: the full `dotnet build` is what proves the whole tree compiles.

## How you know it worked

- A new project or reference appears in `ProjectDirectionPolicy` and
  `ProjectDirectionGateTests` is green.
- `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` is green, which also covers a new file
  that is not committed yet.
- The file you added is in the project the project-direction table allows, and no game-assembly
  reference appeared outside the Game Adapter.

## Related reading

- [Gates and binding rules](gates-and-rules.md) — the rules behind these gates
- [Build, test and deploy](build-and-test.md) — the commands that run them
- [Writing documentation](documentation-standard.md) — where a page goes instead of a stray file
- [Advanced modification policy](../../api/advanced-modification-policy.md) — the four-layer rule and the stability tiers

---

[Documentation](../README.md) > [Contributing](README.md) > Repository map and pitfalls
