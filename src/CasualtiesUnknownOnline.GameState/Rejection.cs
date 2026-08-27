namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// A typed command rejection with a stable reason and a diagnostic message.
/// </summary>
public sealed record Rejection(RejectionReason Reason, string Message)
{
	public static Rejection Of(RejectionReason reason, string message) => new(reason, message);
}
