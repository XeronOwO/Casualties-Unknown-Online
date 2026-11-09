using CasualtiesUnknownOnline.GameState.Domains.World;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The run's ending kind as this client knows it. The host records the branch the game itself resolved
/// (the adapter's ending patch), the run projection stamps it into the run row it advances with, and every
/// client's projection applies the host's kind back here — so a member plays the host's branch instead of
/// its own. The value is cleared when the run ends and a new one starts.
/// </summary>
public static class RunEndingTracker
{
	/// <summary>The kind the game resolved on this client (host), or the host's kind applied from the run (member).</summary>
	public static RunEnding Current { get; private set; } = RunEnding.None;

	/// <summary>True when <see cref="Current"/> came from the authoritative run row rather than this client's own resolution.</summary>
	public static bool FromAuthority { get; private set; }

	/// <summary>The host's game resolved an ending — record it for the run row this client is about to advance.</summary>
	public static void Record(RunEnding ending)
	{
		Current = ending;
		FromAuthority = false;
	}

	/// <summary>The run row arrived from the host (or this client's own committed batch) — adopt its kind.</summary>
	public static void Apply(RunEnding ending)
	{
		Current = ending;
		FromAuthority = ending != RunEnding.None;
	}

	/// <summary>A new run started — forget the previous ending.</summary>
	public static void Clear()
	{
		Current = RunEnding.None;
		FromAuthority = false;
	}
}
