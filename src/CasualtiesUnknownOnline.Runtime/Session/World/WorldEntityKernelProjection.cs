using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Projects the kernel WorldEntities checkpoint into the world-entry
/// application surfaces on the guest. This is the checkpoint-driven rebuild
/// counterpart of the legacy snapshot messages: when a guest restores a
/// checkpoint it raises the same flat fact lists the Game Adapter already
/// knows how to apply, so old snapshot wire can be removed once this path is
/// proven.
/// </summary>
public sealed class WorldEntityKernelProjection
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ISessionControl _session;
	private readonly ITimeSource _time;
	private readonly ILogger<WorldEntityKernelProjection> _log;

	public WorldEntityKernelProjection(
		ItemKernelAuthority kernelAuthority,
		ISessionControl session,
		ITimeSource time,
		ILogger<WorldEntityKernelProjection> log)
	{
		_kernelAuthority = kernelAuthority;
		_session = session;
		_time = time;
		_log = log;
		_kernelAuthority.CheckpointRestored += OnCheckpointRestored;
	}

	/// <summary>Raised when a restored checkpoint carries trap consumptions.</summary>
	public event System.Action<IReadOnlyList<EntityEventMsg>>? TrapSnapshotProjected;

	/// <summary>Raised when a restored checkpoint carries opened lockable entities.</summary>
	public event System.Action<IReadOnlyList<NetVector2Msg>>? OpenedEntitiesProjected;

	/// <summary>Raised when a restored checkpoint carries building-entity health facts.</summary>
	public event System.Action<IReadOnlyList<BuildingEntityHealthEntryMsg>>? BuildingHealthProjected;

	public void Project(GameCheckpoint checkpoint)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		var state = checkpoint.WorldEntities ?? WorldEntityState.Empty;
		if (state.Consumptions.Count > 0)
		{
			var now = _time.NowMs;
			TrapSnapshotProjected?.Invoke(
			[
				.. state.Consumptions.Select(c => new EntityEventMsg
				{
					Kind = (EntityEventKind)c.Kind,
					Extra = c.Extra,
					Position = new NetVector2Msg(c.Position.CenterX, c.Position.CenterY),
					ElapsedSeconds = (now - c.TriggeredAtMs) / 1000f,
				}),
			]);
		}

		if (state.OpenedEntities.Count > 0)
		{
			OpenedEntitiesProjected?.Invoke(
			[
				.. state.OpenedEntities.Select(o => new NetVector2Msg(o.Position.CenterX, o.Position.CenterY)),
			]);
		}

		if (state.BuildingHealth.Count > 0)
		{
			BuildingHealthProjected?.Invoke(
			[
				.. state.BuildingHealth.Select(h => new BuildingEntityHealthEntryMsg
				{
					X = h.Position.CenterX,
					Y = h.Position.CenterY,
					Health = h.Health,
				}),
			]);
		}

		_log.LogDebug(
			"[WorldEntityKernel] projected checkpoint {Revision}: consumptions={Consumptions}, opened={Opened}, health={Health}.",
			checkpoint.GlobalRevision, state.Consumptions.Count, state.OpenedEntities.Count, state.BuildingHealth.Count);
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint) => Project(checkpoint);
}
