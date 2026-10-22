using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The target of one item drag aimed at other players. The user's ruling of 2026-10-22 defines it: the
/// player whose detection range the cursor entered LAST and still occupies. A player whose range the cursor
/// has left is no longer under it, so a range still occupied by the cursor wins again — what the player
/// points at is what the release will mean. When the cursor leaves every range (the path onto the displayed
/// backpack) the last target is KEPT, so the give still names it; only <see cref="IsTargetInRange"/> decides
/// whether a release is a USE.
///
/// <para>
/// Two players entering on the same frame (overlapping circles under a still cursor) are ordered by distance,
/// nearest last, so the still cursor keeps a stable, predictable target; once the cursor moves into another
/// range, that entry wins regardless of distance. The selection is pure so it can be tested without Unity:
/// the adapter feeds it the in-world remote positions and the cursor every frame.
/// </para>
/// </summary>
public sealed class DragTargetSelection
{
	private readonly List<Entry> _entries = [];
	private readonly List<ulong> _inRange = [];
	private readonly List<EnteredThisFrame> _entered = [];

	private float _cursorX;
	private float _cursorY;
	private float _radius;

	/// <summary>The current target, or null before any range has been entered during this drag.</summary>
	public ulong? Target { get; private set; }

	/// <summary>Whether the cursor is currently inside the target's detection range (a release is a USE only then).</summary>
	public bool IsTargetInRange => Target is { } target && IsInRange(target);

	/// <summary>Starts a new drag: no target and no history from the previous one.</summary>
	public void Begin()
	{
		_entries.Clear();
		_inRange.Clear();
		_entered.Clear();
		Target = null;
	}

	/// <summary>Opens one frame with the cursor point and the detection radius every remote is judged against.</summary>
	public void BeginFrame(float cursorX, float cursorY, float radius)
	{
		_cursorX = cursorX;
		_cursorY = cursorY;
		_radius = radius;
		_entered.Clear();
		for (var i = 0; i < _entries.Count; i++)
		{
			var entry = _entries[i];
			entry.Seen = false;
			_entries[i] = entry;
		}
	}

	/// <summary>Offers one remote player this frame. Call once per in-world remote, then <see cref="EndFrame"/>.</summary>
	public void Consider(ulong steamId, float x, float y)
	{
		var dx = x - _cursorX;
		var dy = y - _cursorY;
		var inRange = (dx * dx) + (dy * dy) <= _radius * _radius;
		for (var i = 0; i < _entries.Count; i++)
		{
			var entry = _entries[i];
			if (entry.SteamId != steamId)
			{
				continue;
			}

			entry.Seen = true;
			if (inRange && !entry.InRange)
			{
				entered(steamId, (dx * dx) + (dy * dy));
			}
			else if (!inRange && entry.InRange)
			{
				_inRange.Remove(steamId);
			}

			entry.InRange = inRange;
			_entries[i] = entry;
			return;
		}

		_entries.Add(new Entry { SteamId = steamId, InRange = inRange, Seen = true });
		if (inRange)
		{
			entered(steamId, (dx * dx) + (dy * dy));
		}

		void entered(ulong id, float distanceSquared)
		{
			if (!_inRange.Contains(id))
			{
				_entered.Add(new EnteredThisFrame(id, distanceSquared));
			}
		}
	}

	/// <summary>Closes the frame: applies the entries of this frame in their distance order and refreshes the target.</summary>
	public void EndFrame()
	{
		// A player nobody offered this frame (left the world, stopped streaming) is no longer under the cursor.
		for (var i = _entries.Count - 1; i >= 0; i--)
		{
			var entry = _entries[i];
			if (entry.Seen)
			{
				continue;
			}

			_inRange.Remove(entry.SteamId);
			_entries.RemoveAt(i);
		}

		// Several ranges entered on ONE frame (overlapping circles, still cursor): nearest last, so the
		// still cursor's target is the one it is closest to. A later frame's entry always wins.
		_entered.Sort(static (a, b) => b.DistanceSquared.CompareTo(a.DistanceSquared));
		foreach (var entered in _entered)
		{
			_inRange.Add(entered.SteamId);
		}

		_entered.Clear();
		if (_inRange.Count > 0)
		{
			Target = _inRange[_inRange.Count - 1];
		}
	}

	/// <summary>Whether this player's detection range currently holds the cursor.</summary>
	public bool IsInRange(ulong steamId)
	{
		for (var i = 0; i < _entries.Count; i++)
		{
			if (_entries[i].SteamId == steamId)
			{
				return _entries[i].InRange;
			}
		}

		return false;
	}

	private struct Entry
	{
		internal ulong SteamId;
		internal bool InRange;
		internal bool Seen;
	}

	private readonly record struct EnteredThisFrame(ulong SteamId, float DistanceSquared);
}
