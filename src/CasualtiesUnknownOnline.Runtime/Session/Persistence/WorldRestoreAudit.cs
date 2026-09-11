using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Carries a restore's LIVE-WORLD half back to the caller that started it.
///
/// A restore has two halves in time: <c>TryContinue</c> applies the kernel
/// checkpoint and the Runtime fact tables at the Continue click, and the adapter
/// writes the values only a live world can take at the world-entry seam (the
/// block diff, the game's own partial-damage list, the decided native values).
/// The seam reports here, so "the restore succeeded" is not decided by the click
/// alone: a row the live world refused makes the restore incomplete, and
/// <see cref="Last"/> / <see cref="Reported"/> are how the caller and the
/// player-facing surface hear about it (§6: no silent loss).
///
/// Both halves are Runtime state, so this audit is a plain service — the adapter
/// only calls it (Begin at the click, LiveWrite at the seam) and subscribes to
/// the report.
/// </summary>
public sealed class WorldRestoreAudit
{
	private string _worldId = string.Empty;
	private bool _awaiting;

	/// <summary>The last completed live-world write of a restore, or null before the first one.</summary>
	public WorldRestoreLiveWriteReport? Last { get; private set; }

	/// <summary>The world id the pending restore belongs to ("" when none is pending).</summary>
	public string PendingWorldId => _awaiting ? _worldId : string.Empty;

	/// <summary>True = a restore applied at the click is still waiting for its world-entry seam.</summary>
	public bool AwaitingLiveWrite => _awaiting;

	/// <summary>Raised once per restore's live-world write, complete or not.</summary>
	public event Action<WorldRestoreLiveWriteReport>? Reported;

	/// <summary>
	/// The Continue click applied the cut: from here on the live-world half is
	/// expected. Resets <see cref="Last"/>, because a completed restore's report
	/// must never be read as the new one's outcome.
	/// </summary>
	public void BeginRestore(string worldId)
	{
		_worldId = worldId;
		_awaiting = true;
		Last = null;
	}

	/// <summary>The world-entry seam finished writing the restored cut into the live world.</summary>
	public void LiveWriteFinished(bool complete, IReadOnlyList<string> refused, string summary)
	{
		var report = new WorldRestoreLiveWriteReport(_worldId, complete, refused, summary);
		Last = report;
		_awaiting = false;
		Reported?.Invoke(report);
	}

	/// <summary>
	/// The restore never reached its world-entry seam (the run was superseded, or
	/// the session ended): the pending expectation is dropped without inventing a
	/// report — a restore that never happened is not a restore that succeeded.
	/// </summary>
	public void AbandonRestore()
	{
		_awaiting = false;
		_worldId = string.Empty;
	}
}
