# Command authorization gateway

- Status: Future
- Priority: Medium
- Category: Architecture / authority / security
- Source: Loomi architecture review (2026-09-04)

`AuthorityKind` is recorded on commands/batches, but `GameStateKernel.Execute` does
not enforce whether the caller is eligible. Authorization is currently enforced by
individual Runtime entry points.

Goal: add a thin application-layer gateway in front of the kernel:

- Bind actor to transport sender.
- Enforce `HostOnly`, `Owner`, `Observed`, and related policies centrally.
- Return uniform rejection reasons and emit audit/log context.

Keep Steam/network identity and authorization policy out of the kernel itself so the
kernel remains a pure deterministic state machine.
