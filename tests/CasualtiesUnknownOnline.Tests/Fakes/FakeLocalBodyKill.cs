using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The kill cheat verb's body seam in a test host, which has no game scene: it counts the kills this
/// node's service asked for and answers with the outcome the case needs. It is the same shape as
/// <see cref="FakeLocalBodyReset"/> and shares its convention: one instance serves both nodes of a
/// case, so the assertions read the CALLS and the side that made one is the case's own target.
/// </summary>
internal sealed class FakeLocalBodyKill : ILocalBodyKill
{
	internal int Kills { get; private set; }

	/// <summary>The outcome the next kill answers with (default: the native write, brain health 100 -> 0).</summary>
	internal LocalBodyKillOutcome Outcome { get; set; } = LocalBodyKillOutcome.Killed(100f);

	public LocalBodyKillOutcome Kill()
	{
		Kills++;
		return Outcome;
	}
}
