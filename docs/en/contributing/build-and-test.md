# Build, test and deploy the plugin

[Documentation](../README.md) > [Contributing](README.md) > Build, test and deploy the plugin

---

**After this page** you can build the solution, run the smallest test subset that can catch your
change, deploy the plugin into your own game, and open the log that explains a failure. You need a
development checkout ([Set up a development environment](../start/set-up-dev-environment.md)) and a
.NET SDK able to target `net48`.

## The commands

```bash
dotnet build CasualtiesUnknownOnline.slnx      # every project
dotnet test CasualtiesUnknownOnline.slnx       # the gates and the behavioural suite
dotnet format CasualtiesUnknownOnline.slnx     # formatting; mandatory before a commit
```

`dotnet build` and `dotnet test` must pass before every commit. `dotnet format` is backed by two
build-time mechanisms: `.editorconfig` raises the style rules to error severity, and
`EnforceCodeStyleInBuild` in each project file makes the compiler enforce them, so imperfect style does
not build. Two filters keep the loop short:

- `dotnet test CasualtiesUnknownOnline.slnx --filter "Category!=Integration"` — the fast inner loop.
- `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~<class>"` — one class or one
  family.

A change that touches `src/`, `tests/` or `tools/` runs the whole ladder. A pure documentation change
— no file in those three directories — skips build, test and format: review the diff and commit it
directly. When a documentation change describes a code change, it is committed together with that
code and the gates run in the same commit.

## What the test command runs

`dotnet test` runs two projects:

- the behavioural suite, `tests/CasualtiesUnknownOnline.Tests`;
- the normative gates, `tests/CasualtiesUnknownOnline.NormativeGates.Tests` — the C# replacement for
  the former `tools/check-*.ps1` scripts, which no longer exist.

Run the gate project on its own while iterating: it finishes in seconds, and its failures name the
rule they enforce, so the message usually already says what to change. Which gate enforces which rule
is the [rule-to-gate map](../../evidence/normative-gates.md); the conventions themselves are
[Gates and binding rules](gates-and-rules.md).

## The contract the suite keeps about itself

Tests here are expected to be runnable in any order and in parallel, so the suite carries its own
rules. They are binding, not preferences:

- The runner configuration is `tests/CasualtiesUnknownOnline.Tests/xunit.runner.json`.
- A class that writes a process-global static field joins the `GameAssembly` collection
  (`tests/CasualtiesUnknownOnline.Tests/Patching/GameAssemblyCollection.cs`), enforced by
  `TestIsolationGateTests`: xUnit v2 runs different collections in parallel, so two such classes would
  race on the same Unity/game-assembly static.
- The test composition must not write a per-node rolling log file
  (`tests/CasualtiesUnknownOnline.Tests/Fakes/TestLogging.cs`).
- No test class may exceed 40 real xUnit cases or data rows, `MemberData` expansion included —
  `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` enforces it at runtime. Split an oversized
  class by behaviour family; raising the cap needs the same measured evidence a stage change needs.
- Keep classes small enough that no single class dominates the run, and record a runtime change with the
  three-run median method in [test-parallelization.md](../../evidence/test-parallelization.md).
- Behaviour-family splits share stateless helpers and must never duplicate `MemberData` rows: the kind
  shards have to stay a partition, which a guard test asserts. A class may share a read-only query facade
  as an `IClassFixture` (for example `DirectionProbe`, which exposes only pure queries and keeps its nodes
  private), but never a mutable session or world fixture.
- A class that constructs the production composition root, a full simulation world or replay harness,
  a shared full-stack fixture, the game-assembly reflection host (`GameAssemblyHost`) or real loopback
  sockets (`IpDirectTransport`) carries `[Trait("Category", "Integration")]`. Untagged classes are the
  fast inner loop.
- The runner thread cap stays `1x` (the logical processor count). The measured alternatives and the
  rule that summed test time is never compared across thread settings are in
  [test-parallelization.md](../../evidence/test-parallelization.md) §7.5 and §9.

## Target and packages

- Target framework `net48`, `LangVersion = preview`, nullable enabled, warnings as errors.
- NuGet sources: nuget.org, nuget.bepinex.dev, nuget.samboy.dev.
- `Microsoft.Extensions` stays on the 3.1.x line, the last one that works on `net48`
  (`SourceShapeGateTests.MicrosoftExtensionsPinnedToNet48CompatibleLine`).
- Game assemblies are copyrighted: only the Game Adapter project may reference them, and
  `references/` is gitignored and populated on demand.

## Deploy into your own game

```powershell
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
powershell -ExecutionPolicy Bypass -File tools/verify-deploy.ps1 -GameDir "<game-dir>"
```

- Pass `-GameDir` explicitly. The script refuses a sandbox path, refuses to run while the game is
  running, and deploys only this tree's build output plus the Steam dependencies; it never touches
  BepInEx-owned DLLs.
- `verify-deploy.ps1` compares the deployment against this tree's build output and exits `0` on a
  match; it also prints the deployed `ProductVersion`, whose `+<sha>` suffix names the commit the
  DLLs came from. Deploy after the commit, or the embedded sha points at the previous one.
- Machine-specific paths belong in the gitignored `AGENTS.local.md`, never in a committed file.

## Logs

| Where | What it tells you |
|---|---|
| `BepInEx/LogOutput.log` | chain loading and start-up exceptions |
| `BepInEx/logs/latest.log` | runtime exceptions on the Unity side (`[ERR][Unity:Exception]`) |
| `CUO.log` | CUO's own log; its level comes from the `Logging` configuration section |

## Traps

- A run with `--no-build` can report the whole suite green against an incomplete build output. When a
  run suddenly reports many "missing data file" failures, rebuild before reading the failures as a
  code defect, and always run the last full suite before a commit with the build.
- A project that fails to build leaves its consumers compiling against the last good DLL, which looks
  exactly like a new type being invisible. Check the build output before suspecting the consumer.
- `dotnet format` rewrites files. Never run it inside a review window over a frozen tree, and re-read
  a file after any external tool has touched it.
- Nothing in this suite is a game session. Green tests prove the logic; two real clients on screen are
  the user's acceptance run ([Set up a development environment](../start/set-up-dev-environment.md)).

## How you know it worked

- `dotnet build` reports 0 warnings and 0 errors (warnings are errors here).
- `dotnet test` exits `0`; the gate project alone is green in seconds while you iterate.
- `verify-deploy.ps1` exits `0` and its `ProductVersion` ends in the sha of the commit you built.

## Related reading

- [Gates and binding rules](gates-and-rules.md) — what the gates enforce, and how to add one
- [Repository map and pitfalls](repository-map-and-pitfalls.md) — where a new file belongs
- [Review and delivery](review-and-delivery.md) — the order the commands run in, and the commit convention
- [Test parallelization](../../evidence/test-parallelization.md) — the measured classification and numbers
- [Game-update runbook](../../development/game-update-runbook.md) — what to do the day the game updates

---

[Documentation](../README.md) > [Contributing](README.md) > Build, test and deploy the plugin
