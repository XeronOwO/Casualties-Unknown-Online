using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Kernel-backed trap state-machine projection. Host-side trap edges are
/// mapped through <see cref="TrapStateProfiles"/> and committed as
/// <see cref="RecordTrapStateCommand"/> batches; guests rebuild the same facts
/// from kernel checkpoints/batches.
/// </summary>
public sealed class TrapStateRegistry(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority,
	ITimeSource time)
{
	private readonly ISessionControl _session = session;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ITimeSource _time = time;

	/// <summary>Host only: record a stateful trap edge by committing a kernel command.</summary>
	public void Report(EntityEventKind kind, float x, float y, byte extra)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var phase = TrapStateProfiles.Map(kind);
		if (phase is null)
		{
			return;
		}

		var command = new RecordTrapStateCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			EntityPosition.FromWorld(x, y),
			(int)kind,
			phase.Value,
			extra,
			_time.NowMs);
		_kernelAuthority.TryExecuteHostCommand(command, _session.LocalSteamId, "record-trap-state", out _, out _);
	}

	/// <summary>
	/// Host only: record one live trap trigger as a single atomic kernel
	/// composite. The composite contains the one-shot consumption (when the
	/// kind is one-shot), the state-machine transition (when the kind has a
	/// phase profile), the trap entity's post-trigger building health (when the
	/// caller supplies a destroyed-health observation), and any additional
	/// explosion-diff building-health entries captured in the same trap scope.
	/// </summary>
	public void ReportBatch(EntityEventKind kind, float x, float y, byte extra, float? buildingHealth = null, IReadOnlyList<BuildingEntityHealthEntryMsg>? additionalHealth = null, IReadOnlyList<TrapDropEntryMsg>? drops = null, ulong? dropActor = null)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var commands = new List<GameCommand>();
		if (EntityEventProfiles.IsOneShotConsumption(kind))
		{
			commands.Add(new RecordTrapConsumedCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(_session.LocalSteamId),
				_kernelAuthority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				EntityPosition.FromWorld(x, y),
				(int)kind,
				extra,
				_time.NowMs));
		}

		var phase = TrapStateProfiles.Map(kind);
		if (phase is not null)
		{
			commands.Add(new RecordTrapStateCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(_session.LocalSteamId),
				_kernelAuthority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				EntityPosition.FromWorld(x, y),
				(int)kind,
				phase.Value,
				extra,
				_time.NowMs));
		}

		if (buildingHealth is { } health)
		{
			commands.Add(new RecordBuildingEntityHealthCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(_session.LocalSteamId),
				_kernelAuthority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				EntityPosition.FromWorld(x, y),
				health));
		}

		if (additionalHealth is not null)
		{
			foreach (var entry in additionalHealth)
			{
				commands.Add(new RecordBuildingEntityHealthCommand(
					_kernelAuthority.NextOperationId(),
					new ActorId(_session.LocalSteamId),
					_kernelAuthority.CurrentRunEpoch,
					AuthorityKind.HostOnly,
					EntityPosition.FromWorld(entry.X, entry.Y),
					entry.Health));
			}
		}

		if (drops is not null)
		{
			var actor = dropActor ?? _session.LocalSteamId;
			foreach (var drop in drops)
			{
				commands.Add(new SpawnItemCommand(
					_kernelAuthority.NextOperationId(),
					new ActorId(actor),
					_kernelAuthority.CurrentRunEpoch,
					AuthorityKind.OwnerPredictedHostValidated,
					new ItemIdentity(drop.ItemId, drop.Item.ItemId),
					ItemLocation.World(drop.Position.X, drop.Position.Y),
					0,
					ItemKernelAuthority.ToKernelData(drop.Item)));
			}
		}

		if (commands.Count == 0)
		{
			return;
		}

		var composite = new CompositeGameCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			commands);
		// TryExecuteCommand (not TryExecuteHostCommand) so item spawns inside
		// the composite reach the host's external/projection path and are both
		// broadcast and materialized locally.
		_kernelAuthority.TryExecuteCommand(composite, _session.LocalSteamId, out _, out _);
	}
}
