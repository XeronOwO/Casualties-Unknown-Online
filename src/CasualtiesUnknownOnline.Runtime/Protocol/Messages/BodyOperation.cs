namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Which body operation one <c>/heal</c>-family invocation asks for. The request, the instruction and
/// the result are the same exchange either way — the host judges, the target's own client applies,
/// the answer travels back — so the family carries the operation rather than duplicating the whole
/// control plane for a second verb. The wire value is stated explicitly and never 0: proto omits a
/// 0-valued enum, so an absent field has to read as "no operation" rather than quietly meaning heal.
/// </summary>
public enum BodyOperation
{
	/// <summary>Not stated: the message carries no operation (never sent by this build).</summary>
	None = 0,

	/// <summary>The full heal reset (<see cref="GameAdapter.ILocalBodyReset"/>).</summary>
	Heal = 1,

	/// <summary>Zero brain health (<see cref="GameAdapter.ILocalBodyKill"/>) — the game's own <c>kill</c> verb.</summary>
	Kill = 2,
}
