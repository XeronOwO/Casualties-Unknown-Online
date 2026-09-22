# Tooling

English | [中文](tooling.zh.md)

The repository ships its own verification tools, because the interesting failures happen between two
machines and cannot be reproduced by hand. Everything runs from the command line on the development
machine.

A useful habit is to read the failing test before the log it printed: the gates in this repository
name the rule they enforce, so a failure message usually already says what to change.

## Commands

- `dotnet build CasualtiesUnknownOnline.slnx` — build every project.
- `dotnet test CasualtiesUnknownOnline.slnx` — run the gates and the full suite.
- `dotnet test CasualtiesUnknownOnline.slnx --filter "Category!=Integration"` — the fast inner loop.
- `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~<name>"` — one class or one family.
- `dotnet format CasualtiesUnknownOnline.slnx` — formatting, mandatory before a commit.

## Logs

- `BepInEx/LogOutput.log` — chain loading and startup exceptions.
- `BepInEx/logs/latest.log` — runtime exceptions raised on the Unity side.
- `CUO.log` — CUO's own log, with its level controlled by the `Logging` configuration section.

## The simulation harness

The kernel is deterministic, so a test can replay a recorded scenario without starting the game: the
harness feeds commands into the kernel, compares the commits that come out, and fails at the first
divergence. That is how behaviour is verified without a second client.

## The update-day toolchain

`tools/CasualtiesUnknownOnline.ContractTool` snapshots a game build as metadata and classifies what
changed between two builds, which turns a game update into a reviewable diff instead of a debugging
session. The procedure is `docs/development/game-update-runbook.md`.
