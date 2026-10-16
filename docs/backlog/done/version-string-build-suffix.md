# The version a player and a log reader sees should carry the build's commit

- Status: Done — the three surfaces name the same commit on the machine (`docs/evidence/acceptance/build-stamp-surfaces-20261017-e.md`), and a build from a dirty tree marks itself.
- Priority: Low
- Category: Diagnostics / release hygiene
- Source: the user's 2026-10-07 backlog request — the version number should carry the commit-hash build
  suffix, so a build can be traced back to its source.
- Related: `tools/verify-deploy.ps1` (prints the deployed plugin's `ProductVersion` with its `+<sha>`),
  `docs/acceptance/workflow.md` (the artifact identity every run records),
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldCutWriter.cs` (the archive already records
  `AssemblyInformationalVersion`), `docs/en/contributing/build-and-test.md`

## What is observed

- The suffix already exists **inside the artifact**: the plugin assembly's `ProductVersion` is
  `0.1.0+<full commit sha>`, `verify-deploy.ps1` prints it, and a world archive records
  `AssemblyInformationalVersion`, so an archive can be traced to the build that wrote it.
- What does **not** carry it is the surface a player or a support reader actually looks at: the plugin
  registers with `MyPluginInfo.PLUGIN_VERSION` (the numeric version only) and its startup line logs the GUID
  alone (`Plugin {PluginGuid} is loaded!`), and the Online UI's Home page has no version row. So an
  in-game screenshot, a player's bug report or a fresh log file cannot name the build.

## Required work

1. Put the full version (numeric + `+<sha>`) where it is read: the startup log line, and one row on the Online
   UI's Home page beside the existing session facts. One source of truth — read
   `AssemblyInformationalVersionAttribute` (as `WorldCutWriter` already does) rather than composing a second
   string from the BepInEx metadata.
2. Decide the display length for the UI (the full sha in a log line, a short prefix on screen) and state it,
   so a player reading a screenshot and a developer reading a log can be matched to each other.
3. Keep the release form honest: the suffix must be absent or clearly marked on a build that is not from a
   commit (a local dirty tree), rather than showing a sha that does not describe it.
4. Verify by deploying once and reading the three surfaces (log line, UI row, `verify-deploy.ps1` output) and
   checking they name the same commit.

## Non-goals

- Not a versioning scheme change: no new numbering policy, no semver bump rules — the existing
  `0.1.0+<sha>` shape is what gets surfaced.
- Not a crash-report or telemetry feature: it is a string in two places.
## What landed (2026-10-17)

- **One reader**: `src/CasualtiesUnknownOnline.Runtime/Diagnostics/BuildIdentity.cs` reads
  `AssemblyInformationalVersionAttribute` and offers the two forms. The startup line and every archive take
  the whole stamp; the Home row takes `Short` (`ShortRevisionLength = 7`), which keeps EVERY marker the stamp
  carries — the review caught the first version dropping `-dirty`, which would have made the one surface a
  player reads unable to tell a dirty build from a clean one.
- **The dirty marker is real**: the SDK already stamps `+<SourceRevisionId>` from git, so a build from a tree
  with uncommitted work carried a sha that did not describe it. A target under `src/` appends `-dirty` to that
  same value. Its exit code decides rather than its captured output (`ConsoleToMSBuild` collects stderr, so a
  missing or refusing git would otherwise mark a clean checkout), and untracked sources COUNT — a file the
  build compiles but HEAD does not have is exactly the case the marker exists for.
- **The gate that declared its own blind spot now reads it**: `GameAssemblyReferenceGateTests` documented that
  an imported `Directory.Build.props` was outside its scan "and the tree carries no such file today". This
  change adds one, so the gate reads the convention files wherever they sit and its declaration (and
  `docs/architecture/guards.md`) says so.
- **Required work 4 is open**: the run that reads the three surfaces (startup line, Home row, and
  `verify-deploy.ps1`'s output naming the same commit) is the next batch, which is why this ticket is in
  `review/`. What the run must also settle: the Home row's placement is judged from a captured frame.