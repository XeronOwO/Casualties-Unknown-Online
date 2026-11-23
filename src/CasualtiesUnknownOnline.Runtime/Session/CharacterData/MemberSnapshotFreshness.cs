using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Host side: how old each present member's stored character snapshot is.
///
/// The cut writes a member's row from the snapshot the store holds, and that snapshot only
/// moves when the member's own report arrives (the 1 Hz report path). A cut taken inside that
/// second therefore writes the row from before the member's last pickup — measured 2026-11-24:
/// the member's own tree read one item at the cut while the archive carried none, and the row
/// only went green when the cut waited for the next report. This class is the owner of that age,
/// so the cut's transient policy can wait for a fresh glance instead of writing a row it knows
/// is old.
///
/// The clock is injected so the age is a testable fact rather than a reading of the wall.
/// </summary>
public sealed class MemberSnapshotFreshness(Func<int>? clock = null)
{
	/// <summary>A snapshot older than this is stale enough that the cut waits for the next report.</summary>
	public const int FreshMs = 1000;

	/// <summary>A member silent for longer than this is not waited for: a paused or stalled client must not hold the cut.</summary>
	public const int AliveMs = 6000;

	private readonly Func<int> _now = clock ?? (() => Environment.TickCount);
	private readonly Dictionary<ulong, int> _reported = [];
	private readonly Dictionary<ulong, int> _written = [];

	/// <summary>Host: the member's report arrived at this instant (the store kept it as the snapshot).</summary>
	public void NoteReported(ulong steamId)
	{
		var now = _now();
		_reported[steamId] = now;
		_written[steamId] = now;
	}

	/// <summary>Host: an authoritative write replaced the snapshot at this instant (the applier's own writes).</summary>
	public void NoteWritten(ulong steamId) => _written[steamId] = _now();

	/// <summary>The peer left: its age is no longer the cut's business.</summary>
	public void Forget(ulong steamId)
	{
		_reported.Remove(steamId);
		_written.Remove(steamId);
	}

	/// <summary>
	/// True when the peer's report is recent enough to expect another (so waiting is not a stall)
	/// and the snapshot it produced is older than <see cref="FreshMs"/>.
	/// </summary>
	public bool IsStale(ulong steamId)
	{
		var now = _now();
		if (!_reported.TryGetValue(steamId, out var reported) || now - reported is < 0 or > AliveMs)
		{
			return false;
		}

		return !_written.TryGetValue(steamId, out var written) || now - written > FreshMs;
	}
}
