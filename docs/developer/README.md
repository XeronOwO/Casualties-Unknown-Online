# Developer guide

English | [中文](README.zh.md)

This guide is for someone who is going to build, patch or extend CUO and has not read the source
yet. It explains the shape of the mod, how the pieces talk to each other and where to start; the
exact contracts stay in the reference layer, which this guide links to instead of copying.

Read it in order the first time. Afterwards, the reference map at the end is the fastest way to
reach the document you actually need.

## The pages

- [Overview](overview.md) — the mod's shape: a stable runtime, a replaceable adapter and a deterministic kernel.
- [Protocol](protocol.md) — the four envelopes, joining, the state stream and the version check.
- [Sync model](sync-model.md) — what the host owns, what each client judges, and how conflicts are settled.
- [Layers](layers.md) — Runtime, Game Adapter and Abstractions: what you may patch, and what is not promised.
- [Tooling](tooling.md) — logs, the simulation harness, and how to run and filter the test suite.
- [Saves](saves.md) — the world archive layout and the restore path.
- [Known issues](known-issues.md) — declared gaps and where each one is tracked.
- [Reference map](reference-map.md) — the English reference layer in reading order.

## How to use this guide

A page here explains a mechanism in the terms the code uses, so the identifiers you read are the
identifiers you will find in the tree. When a page needs more depth than it can carry, it links into
the reference layer rather than restating it.

## Building the mod

The build is an ordinary .NET solution: `dotnet build CasualtiesUnknownOnline.slnx`, then
`dotnet test CasualtiesUnknownOnline.slnx` for the gates and the suite. [AGENTS.md](../../AGENTS.md)
at the repository root holds the binding commands, and [operations](../operations/README.md) covers
deployment and the local toolchain.
