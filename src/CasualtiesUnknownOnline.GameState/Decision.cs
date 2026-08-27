namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// The kernel response to a command: either an accepted committed batch or a
/// typed rejection.
/// </summary>
public sealed record Decision(CommittedBatch? Batch, Rejection? Rejection)
{
	public bool IsAccepted => Batch is not null;

	public static Decision Accepted(CommittedBatch batch) => new(batch, null);

	public static Decision Rejected(Rejection rejection) => new(null, rejection);
}
