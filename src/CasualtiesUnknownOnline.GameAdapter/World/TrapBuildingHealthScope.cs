using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Collects building-entity health side effects while the host applies one
/// guest-triggered trap event. The explosion diff runs inside the scope, so
/// the resulting health facts can be folded into the same atomic kernel trap
/// batch instead of committing as separate kernel updates.
/// </summary>
internal sealed class TrapBuildingHealthScope : IDisposable
{
	private static TrapBuildingHealthScope? _current;
	private readonly List<BuildingEntityHealthEntryMsg> _entries = [];

	private TrapBuildingHealthScope()
	{
	}

	internal static TrapBuildingHealthScope Begin()
	{
		if (_current is not null)
		{
			throw new InvalidOperationException("a trap building-health scope is already active");
		}

		var scope = new TrapBuildingHealthScope();
		_current = scope;
		return scope;
	}

	internal IReadOnlyList<BuildingEntityHealthEntryMsg> Entries => _entries;

	/// <summary>Adds a health observation when a scope is active; returns false when no scope is active so the caller keeps the normal direct kernel report.</summary>
	internal static bool TryAdd(float x, float y, float health)
	{
		if (_current is null)
		{
			return false;
		}

		_current._entries.Add(new BuildingEntityHealthEntryMsg
		{
			X = x,
			Y = y,
			Health = health,
		});
		return true;
	}

	public void Dispose()
	{
		_current = null;
		_entries.Clear();
	}
}
