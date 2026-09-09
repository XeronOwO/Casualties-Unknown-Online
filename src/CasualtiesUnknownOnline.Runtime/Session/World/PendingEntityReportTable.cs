using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest-side table of runtime entity creations whose report the host has
/// not answered yet (sync-coverage audit E3): <see cref="RuntimeEntityKey"/> →
/// the exact creation message this side reported. The host→guest direction
/// heals a lost relay with the absolute runtime-entity table, but a lost
/// guest→host creation report had no recovery at all — the host never learned
/// the entity, so its absolute table omitted it and could not heal either side.
/// This table is that recovery's source. An entry is dropped when the host
/// answers for that creation (its relay echo or the absolute snapshot) or when
/// the local copy dies, and cleared when a new world/layer baseline is applied
/// or the session ends. Bounded: at the cap a NEW key is refused and the caller
/// logs the overflow episode once (never a silent drop; an existing key still
/// updates).
/// </summary>
public sealed class PendingEntityReportTable
{
	/// <summary>Same bound as the host's accepted-creation table — a mod storm cannot grow the table without bound.</summary>
	public const int DefaultCap = RuntimeEntityRegistry.DefaultCap;

	private readonly Dictionary<RuntimeEntityKey, PendingEntityReport> _entries = [];
	private readonly int _cap;

	public PendingEntityReportTable()
		: this(DefaultCap)
	{
	}

	/// <summary>Test seam (InternalsVisibleTo): the cap is a policy constant; tests exercise the overflow with a small bound.</summary>
	internal PendingEntityReportTable(int cap)
	{
		_cap = cap;
	}

	public int Count => _entries.Count;

	public int Cap => _cap;

	/// <summary>
	/// Upsert the creation's pending report — a re-report of the same creation
	/// supersedes the older record. Returns false when the cap refused a NEW key
	/// (an existing key always updates).
	/// </summary>
	public bool Report(EntitySpawnedMsg msg)
	{
		var key = RuntimeEntityKey.From(msg);
		if (!_entries.ContainsKey(key) && _entries.Count >= _cap)
		{
			return false;
		}

		_entries[key] = new PendingEntityReport(key, msg, 0);
		return true;
	}

	/// <summary>Drop the creation — the host has answered for it, or the local copy died. Returns whether an entry was removed.</summary>
	public bool Remove(RuntimeEntityKey key) => _entries.Remove(key);

	/// <summary>Count one fallback re-send for the creation; returns the new attempt count (0 when the entry is already gone).</summary>
	public int RecordAttempt(RuntimeEntityKey key)
	{
		if (!_entries.TryGetValue(key, out var entry))
		{
			return 0;
		}

		_entries[key] = entry with { Attempts = entry.Attempts + 1 };
		return _entries[key].Attempts;
	}

	/// <summary>The world baseline was replaced or the session ended — every pending report belongs to the previous world.</summary>
	public void Clear() => _entries.Clear();

	/// <summary>The unacknowledged creations to re-report. Every entry is an independent idempotent report, so the order is irrelevant.</summary>
	public IReadOnlyList<PendingEntityReport> Entries => [.. _entries.Values];
}
