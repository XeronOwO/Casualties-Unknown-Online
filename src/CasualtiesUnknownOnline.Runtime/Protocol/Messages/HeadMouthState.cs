namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The owner's current head/mouth sprite state as seen by the game's own
/// <c>FacialExpression.Update</c>. The remote clone's local clone-state
/// (inherited slot contents, limb latches, eat-time) can disagree with the
/// owner's visual state, so the owner's actual mouth choice is carried on the
/// 1 Hz character snapshot and replayed on the clone instead of being derived
/// independently from proxy inputs.
/// </summary>
public enum HeadMouthState : byte
{
	/// <summary>Normal closed-head sprite (<c>defaultHead</c>).</summary>
	Closed = 0,

	/// <summary>Half-open eating/drinking sprite (<c>defaultHeadMouthHalf</c>).</summary>
	HalfOpen = 1,

	/// <summary>Open mouth sprite (<c>defaultHeadMouth</c>).</summary>
	Open = 2,
}
