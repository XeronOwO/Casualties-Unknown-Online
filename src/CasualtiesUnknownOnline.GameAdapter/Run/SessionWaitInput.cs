using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Everything the wait decision needs, as plain values. The decision used to be read
/// straight off the live session inside the coordinator, which made the two mistakes an
/// adversarial review found untestable — both of them were claims about OTHER machines
/// built from this machine's own state, and both lived in a getter no test could reach.
/// Taking the facts as input puts the decision where a test can drive it.
/// </summary>
internal sealed record SessionWaitInput(
	bool IsHost,
	ulong LocalSteamId,
	ulong HostSteamId,
	IReadOnlyList<SessionWaitMember> Members,
	bool HostInWorld,
	int GateRemainingSeconds,
	int MsSinceHostInWorld);
