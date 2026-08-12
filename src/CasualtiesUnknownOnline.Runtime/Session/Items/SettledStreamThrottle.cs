using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The host's position-stream throttle (PURE — no Unity): a settled item
/// (the adapter feeds the <see cref="ItemMotionState"/> verdict) re-aligns at
/// 1 Hz instead of every 10 Hz tick, except the motion→rest EDGE which forces
/// one immediate tick — the guest's copy stops by itself but its final resting
/// spot must converge on the authority's, and waiting for the round would
/// leave the end state open for up to a second. Throttling, never filtering:
/// a settled item still rides the stream, just at 1/10 the rate (its payload
/// is identical every tick anyway).
/// </summary>
internal sealed class SettledStreamThrottle
{
	private const int SettledIntervalTicks = 10;

	private int _settledTick;
	private bool _settledRound;
	private readonly HashSet<ulong> _settledItems = [];

	internal int SettledCount => _settledItems.Count;

	/// <summary>Start one pump round: the 1 Hz re-align round flag is derived
	/// from the tick counter (every 10th pump — global, not per item).</summary>
	internal void BeginPump()
	{
		_settledTick++;
		_settledRound = _settledTick % SettledIntervalTicks == 0;
	}

	/// <summary>Whether this item is sent this pump: a moving item always, a
	/// settled one on its motion→rest edge or on the 1 Hz round.</summary>
	internal bool ShouldSend(ulong itemId, bool settled)
	{
		if (!settled)
		{
			_settledItems.Remove(itemId);
			return true;
		}

		return _settledItems.Add(itemId) || _settledRound; // edge || 1 Hz round
	}
}
