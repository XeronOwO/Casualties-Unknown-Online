using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Collects building-entity health side effects while the host applies one
/// guest-triggered trap event. The explosion diff runs inside the scope, so
/// the resulting health facts can be folded into the same atomic kernel trap
/// batch instead of committing as separate kernel updates.
/// <para>
/// It ALSO collects the drop table of every entity the host's own application
/// damaged, read off the live component at that moment
/// (<see cref="GameBuildingDropTable.ForEntity"/>). That is the capture point
/// the reported drops are later judged against: the game destroys a dead entity
/// in its own next <c>Update</c>, so after the apply there is no component left
/// to read — and the host's own blast is the only statement of WHICH entities
/// this event touched that does not come from the reporter.
/// </para>
/// <para>
/// The entity whose table the caller already read before the apply (the trap
/// entity itself) is EXCLUDED from this second read: the same table must not
/// reach the bound twice. <see cref="DropBound"/> is one ROW per roll, so a
/// duplicated set would admit a second copy of a drop the game rolled once.
/// </para>
/// </summary>
internal sealed class TrapBuildingHealthScope : IDisposable
{
	private static TrapBuildingHealthScope? _current;
	private readonly BuildingEntity? _dropTableReadBeforeApply;
	private readonly List<BuildingEntityHealthEntryMsg> _entries = [];
	private readonly List<DropBound> _dropRows = [];
	private readonly ILogger? _log;
	private int _marked;

	private TrapBuildingHealthScope(BuildingEntity? dropTableReadBeforeApply, ILogger? log)
	{
		_dropTableReadBeforeApply = dropTableReadBeforeApply;
		_log = log;
	}

	/// <summary>
	/// Opens the scope. <paramref name="dropTableReadBeforeApply"/> is the entity
	/// whose drop table the caller already captured before the apply (the trap
	/// entity), so this scope does not add its rows a second time.
	/// </summary>
	internal static TrapBuildingHealthScope Begin(BuildingEntity? dropTableReadBeforeApply = null, ILogger? log = null)
	{
		if (_current is not null)
		{
			throw new InvalidOperationException("a trap building-health scope is already active");
		}

		var scope = new TrapBuildingHealthScope(dropTableReadBeforeApply, log);
		_current = scope;
		return scope;
	}

	internal IReadOnlyList<BuildingEntityHealthEntryMsg> Entries => _entries;

	/// <summary>The drop rows of every entity the host's own application damaged while this scope was open — the union the event's reported drops are judged against.</summary>
	internal IReadOnlyList<DropBound> DropRows => _dropRows;

	/// <summary>
	/// Adds a health observation and the entity's own drop rows when a scope is
	/// active; returns false when no scope is active so the caller keeps the
	/// normal direct kernel report. The entity is read HERE, while it still
	/// exists: the death branch destroys it on its own next update.
	/// </summary>
	internal static bool TryAdd(BuildingEntity entity, float x, float y, float health)
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
		if (!ReferenceEquals(entity, _current._dropTableReadBeforeApply))
		{
			_current._dropRows.AddRange(GameBuildingDropTable.ForEntity(entity));

			// The side that OBSERVED the death rolls (decision 184/265): this blast is the MEMBER's — the host is only
			// APPLYING the event it reported — so the host's own copy must not roll a second set for the same death.
			// Marking the entity as a remote death is what the game's death branch reads before it rolls
			// (BuildingEntity.cs:56-123), and the support-loss path uses the same mark for the same reason.
			if (entity.GetComponent<RemoteEntityDeath>() == null) // Unity object — ==
			{
				RemoteEntityDeath.Mark(entity, replayAnimalDeath: false);
				_current._marked++;
			}
		}

		return true;
	}

	public void Dispose()
	{
		if (_marked > 0)
		{
			_log?.LogInformation("[TrapBlast] marked {Count} blast victim(s) as remote deaths — this host applies a member's event, so the member's own roll is the world's set and this client only removes the entities.", _marked);
		}

		_current = null;
		_entries.Clear();
		_dropRows.Clear();
	}
}
