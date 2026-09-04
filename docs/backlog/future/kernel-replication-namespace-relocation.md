# Kernel replication namespace relocation

- Status: Future
- Priority: Low
- Category: Architecture / maintainability
- Source: Loomi architecture review (2026-09-04)

Kernel protocol/replication services currently live under `Runtime/Session/Items/`
(`KernelProtocolService`, `KernelProtocolCommandHandler`, `KernelSaveFileStore`, and
related types), even though they are no longer item-specific after the full-domain
kernel migration.

Goal: move the kernel replication/save/control surface to a neutral namespace such as
`Runtime/Session/Kernel` or `Runtime/Replication`, update references and tests, and
keep behavior unchanged. If combined with a real responsibility split, do so; do not
use the move to merely avoid architecture gates.
