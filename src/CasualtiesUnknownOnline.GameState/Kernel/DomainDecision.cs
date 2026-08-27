using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// A domain module's response to a command: accepted event drafts or a typed
/// rejection.
/// </summary>
internal sealed record DomainDecision(
	bool Accepted,
	IReadOnlyList<GameEvent> Events,
	Rejection? Rejection)
{
	public static DomainDecision Accept(params GameEvent[] events) => new(true, events, null);

	public static DomainDecision Reject(RejectionReason reason, string message) =>
		new(false, [], Rejection.Of(reason, message));
}
