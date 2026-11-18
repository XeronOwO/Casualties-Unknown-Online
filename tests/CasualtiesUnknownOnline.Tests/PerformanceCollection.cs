using Xunit;

namespace CasualtiesUnknownOnline.Tests;

/// <summary>
/// The performance rows measure the process's own CPU time, so they must not share the process with other tests: a full run
/// parallelizes collections, and the other collections' CPU would land in this one's reading (measured 2026-11-18: the widest
/// line read 2.564 ms wall under load and 1.719 ms of process CPU under load, against a 1.6 ms budget).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
	/// <summary>The name a performance row's <see cref="CollectionAttribute"/> uses.</summary>
	public const string Name = "Performance";
}
