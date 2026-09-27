using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The in-process driver's black-box tests each spawn their own <c>powershell.exe</c> and the driver carries
/// its own connect/eval budgets. Run in parallel with the rest of the suite, the spawned process starves:
/// the same cases that pass in isolation (20/20) failed three full-suite runs with exit 3 — a loaded
/// machine's slow PowerShell start was read as an unreachable endpoint. The other two tool harnesses
/// (preflight, item-trace) have no internal budget, so a slow machine only makes them slow; they were
/// audited and stay parallel. This collection is non-parallel so load cannot turn a contract into a
/// timeout.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ToolProcessCollection
{
	public const string Name = "tool-process";
}
