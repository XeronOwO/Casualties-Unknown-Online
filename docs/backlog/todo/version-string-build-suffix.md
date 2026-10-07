# The version a player and a log reader sees should carry the build's commit

- Status: Todo
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
