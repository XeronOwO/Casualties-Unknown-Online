using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The per-run-entry hand-over ledger. A run's invitation pump re-sends the entry instruction
/// every second while a member stays outside the world, but the character hand-over is one shot
/// per entry: re-staging it would replace the receiver's queued restore and restart its two-pass
/// apply on a body that may already be half-way through one. The entry edge
/// (<see cref="BeginEntry"/>) makes the next hand-over a new one, so the same member can be handed
/// its character again in the next run.
/// </summary>
internal sealed class CharacterEntryHandover
{
	private readonly Dictionary<ulong, long> _handed = [];
	private long _entry;

	/// <summary>A new run entry is being announced: every member's hand-over is due again.</summary>
	internal void BeginEntry()
	{
		_entry++;
		_handed.Clear();
	}

	/// <summary>Whether this member has not been handed its character in the current entry.</summary>
	internal bool IsDue(ulong peer) => !_handed.TryGetValue(peer, out var entry) || entry != _entry;

	/// <summary>The hand-over landed: the member is not due again until the next entry.</summary>
	internal void MarkHanded(ulong peer) => _handed[peer] = _entry;

	/// <summary>Session ended: a new session's entries start clean.</summary>
	internal void Clear() => _handed.Clear();
}
