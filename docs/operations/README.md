# Operations, Tooling, and Deployment

This is the operations layer for contributors and maintainers. Machine-specific
paths, sandbox details, and personal environment facts belong in
`AGENTS.local.md` (gitignored); this page only contains shared, committable
operations guidance.

## Build and verification

The authoritative command list is in [`[../AGENTS.md`](../../AGENTS.md). In brief:

- `tests/CasualtiesUnknownOnline.NormativeGates.Tests` contains the C# gates that
  replaced the former `tools/check-*.ps1` scripts: strict structure/Phase E guards,
  event-replay matrix, entity-event dispatch, absolute-path scan, and delivery
  checklist. They run as part of `dotnet test`.
- The former PowerShell check scripts were removed after their logic was ported
  into this test project.
- Pure documentation-only changes skip these gates; run `git diff --check` and
  review the diff before committing.

[See `verification.md`](../evidence/verification.md) for the evidence layer,
[`delivery-checklist.md`](../evidence/delivery-checklist.md) for the delivery gate, and
[`[../AGENTS.md`](../../AGENTS.md) for the binding commands.

## Deployment

The plugin is deployed with:

```powershell
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
```

- Always pass `-GameDir` explicitly.
- `tools/deploy.ps1` refuses sandbox paths and deploys only build output DLLs
  plus Steam dependencies; it never deploys BepInEx-owned DLLs.
- The current machine game path and sandbox setup are recorded in
  `AGENTS.local.md` and are never committed.

## Git discipline

- All commits are GPG-signed. Do not bypass with `--no-gpg-sign` or
  `-c commit.gpgsign=false`.
- Keep the working tree clean at the end of a work session.
- Pure documentation commits should be small, reviewable batches and pass
  `git diff --check`.
- If a documentation change accompanies code, run the full gates in the same
  commit.

## Local development tools

- **HotRepl** — runtime C# evaluation/debug via a local ws endpoint. The hookup
  and port conventions are machine-specific and live in `AGENTS.local.md`.
- **Sandboxie dual-instance** — used for host/guest runtime testing. The sandbox
  paths and shadow-cache procedure are in `AGENTS.local.md`; never deploy into a
  sandbox path with `tools/deploy.ps1`.
- **Logs** — BepInEx `LogOutput.log`, `BepInEx/logs/latest.log`, and `CUO.log`;
  their exact locations and troubleshooting notes are in `AGENTS.local.md`.

## Documentation map

The full semantic doc map is [`[docs/README.md`](README.md). The proof/evidence
[layer is `docs/evidence/verification.md`](../evidence/verification.md), and the open-work view is
[`docs/backlog/README.md`](../backlog/README.md).
