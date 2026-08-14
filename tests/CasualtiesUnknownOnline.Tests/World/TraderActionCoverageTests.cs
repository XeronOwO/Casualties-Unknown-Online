using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trade-action coverage guard: the random interaction simulation must
/// exercise EVERY <see cref="TraderActionKind"/> value and never an invalid
/// one. The generator's pool is reflection-derived from the enum, so a new
/// action kind is automatically included — this guard locks that the pool
/// stays exactly the enum (a kind silently dropped here would never run
/// through the wire path + convergence assertions).
/// </summary>
public class TraderActionCoverageTests
{
	[Fact]
	public void RandomActionPool_CoversEveryKind_AndNeverAnInvalidValue()
	{
		var all = Enum.GetValues(typeof(TraderActionKind)).Cast<TraderActionKind>().ToHashSet();
		var pool = TradeSimulationTests.RandomActionKinds.ToHashSet();

		var missing = all.Except(pool).ToList();
		var invalid = pool.Except(all).ToList();

		Assert.True(missing.Count == 0,
			$"the random simulation must exercise every TraderActionKind; missing: [{string.Join(", ", missing)}]");
		Assert.True(invalid.Count == 0,
			$"the random simulation must never generate an invalid kind; invalid: [{string.Join(", ", invalid)}]");
	}
}
