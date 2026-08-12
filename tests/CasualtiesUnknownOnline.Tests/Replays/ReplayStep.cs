namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// One parsed replay-file step: the virtual-clock timestamp (@ms), the action
/// name and its raw arguments, plus the source line (diagnostics — a failing
/// step reports "file:line", and the file's OperationTrace provenance lines
/// make the archive auditable). The parser validates the structure (known
/// action, argument count, timestamp order); the runner owns the semantics
/// (argument conversion, world effects, assertions).
/// </summary>
internal sealed record ReplayStep(int Ms, string Action, string[] Args, int Line)
{
	public override string ToString() => $"@{Ms} {Action} {string.Join(" ", Args)}";
}
