using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The Runtime half of the cut's transient observation: the in-flight state the
/// CUO services hold at the cut instant, in <see cref="WorldTransientPolicy"/>'s
/// own vocabulary. It is a port because the cut's decision must not depend on
/// WHICH services happen to own the state — the production implementation is a
/// read-only query over them (<see cref="WorldCutTransientProbe"/>), and the
/// Game Adapter's half arrives as an argument at the seam.
/// </summary>
public interface IWorldCutTransientProbe
{
	/// <summary>The Runtime-owned in-flight classes, zero-count rows included (the policy decides what a zero means).</summary>
	IReadOnlyList<WorldTransientCount> Capture();
}
