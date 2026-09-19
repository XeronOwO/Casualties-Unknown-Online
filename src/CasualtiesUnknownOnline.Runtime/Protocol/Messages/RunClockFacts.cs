namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The adapter-side read of the two clocks <see cref="RunFactsMsg"/> carries: what the
/// live world holds right now. It is an ordinary Runtime value rather than a wire type —
/// the kernel run baseline's identity is stamped onto the message when it is sent, so this
/// record never has to guess it.
///
/// <see cref="Failure"/> is the ABSENCE of the value (no live world can be read): a
/// receiver then keeps its own clock and layer timer, because a zero here is a value
/// nobody read.
/// </summary>
/// <param name="RunClockBase">The run's total time in seconds (<c>savedRunTime + realTimeElapsed</c>).</param>
/// <param name="LayerTimeSpent">The layer's <c>layerTimeSpent</c>, seconds.</param>
/// <param name="MaxTimePerLayer">The layer's <c>maxTimePerLayer</c>, seconds.</param>
/// <param name="Failure">Why the values could not be read; null when the read succeeded.</param>
public readonly record struct RunClockFacts(
	float RunClockBase,
	float LayerTimeSpent,
	float MaxTimePerLayer,
	string? Failure)
{
	/// <summary>No live world to read: the absence of the facts, never a zero pair.</summary>
	public static RunClockFacts Unreadable => new(0f, 0f, 0f, "no live world is present, so the run clock and the layer timer could not be read");
}
