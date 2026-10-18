using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The heal cheat verb's body seam in a test host, which has no game scene: it counts the resets
/// this node's service asked for and answers with the outcome the case needs. The host and member
/// halves of one case share this instance, exactly as two nodes of a suite share the fake network —
/// the assertions therefore read the CALLS, and the side that made one is identified by the case's
/// own target.
/// </summary>
internal sealed class FakeLocalBodyReset : ILocalBodyReset
{
	internal int Resets { get; private set; }

	/// <summary>The instruction id the last reset was asked to answer (null = the local console triggered it).</summary>
	internal ulong? LastInstructionId { get; private set; }

	/// <summary>The outcome the next reset answers with (default: an applied, alive, whole reset).</summary>
	internal LocalBodyResetOutcome Outcome { get; set; } = LocalBodyResetOutcome.AppliedNow(wasDead: false, restoredLimbs: 0, hollowCleared: false);

	public LocalBodyResetOutcome Reset(ulong? instructionId)
	{
		Resets++;
		LastInstructionId = instructionId;
		return Outcome;
	}
}
