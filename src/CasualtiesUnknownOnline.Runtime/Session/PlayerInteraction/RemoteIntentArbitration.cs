using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// First-writer-wins arbitration for the native inventory intents: one peer at
/// a time may operate one item instance. The admitted requester keeps the item
/// for a lease — the item's next authoritative report normally arrives well
/// inside it — and the same requester may send its next gesture immediately,
/// because the owner's own native guards serialize one client's successive
/// calls. A competing peer is refused while the lease holds, so two players
/// dragging the same item cannot both be forwarded and then fight inside the
/// owner's scene.
///
/// The clock is passed in (<see cref="Time.ITimeSource"/> in production, a
/// virtual one in tests): no session state, no per-frame polling, no hidden
/// timer — the table only ever holds items whose lease has not expired.
/// </summary>
internal sealed class RemoteIntentArbitration
{
	/// <summary>How long one admitted intent keeps its item reserved against a competing peer.</summary>
	internal const long LeaseMs = 2000;

	private readonly Dictionary<ulong, (ulong Requester, long SinceMs)> _inFlight = [];

	/// <summary>The items currently held by an admitted intent — the table is observable for the tests and the diagnostics path.</summary>
	internal int Count => _inFlight.Count;

	/// <summary>
	/// Admit this requester's intent for the item, or refuse it because another
	/// peer holds the item's lease (<paramref name="holder"/> names that peer).
	/// </summary>
	internal bool TryAdmit(ulong requester, ulong itemInstanceId, long nowMs, out ulong holder)
	{
		PurgeExpired(nowMs);
		if (_inFlight.TryGetValue(itemInstanceId, out var current) && current.Requester != requester)
		{
			holder = current.Requester;
			return false;
		}

		_inFlight[itemInstanceId] = (requester, nowMs);
		holder = 0;
		return true;
	}

	private void PurgeExpired(long nowMs)
	{
		if (_inFlight.Count == 0)
		{
			return;
		}

		List<ulong>? expired = null;
		foreach (var entry in _inFlight)
		{
			if (nowMs - entry.Value.SinceMs >= LeaseMs)
			{
				(expired ??= []).Add(entry.Key);
			}
		}

		if (expired is null)
		{
			return;
		}

		foreach (var itemInstanceId in expired)
		{
			_inFlight.Remove(itemInstanceId);
		}
	}
}
