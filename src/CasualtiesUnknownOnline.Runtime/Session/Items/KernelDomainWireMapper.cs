using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Pure wire mappings for non-item kernel domains. Kept separate from
/// <see cref="KernelWireMapper"/> so the item/wire mapper stays under the
/// architecture line gate.
/// </summary>
public static class KernelDomainWireMapper
{
	public static WireRunState ToWireRun(RunState run) =>
		new()
		{
			RunId = run.RunId,
			RandomState = run.RandomState,
			BiomeOverride = run.BiomeOverride,
			BiomeDepth = run.BiomeDepth,
			TotalTraveled = run.TotalTraveled,
			LoadedRun = run.LoadedRun,
			LayerIndex = run.LayerIndex,
			RunSettings = [.. (run.RunSettings ?? []).Select(ToWireRunSetting)],
		};

	public static RunState FromWireRun(WireRunState run) =>
		new(
			run.RunId,
			run.RandomState,
			(byte)run.BiomeOverride,
			(byte)run.BiomeDepth,
			run.TotalTraveled,
			run.LoadedRun,
			run.RunSettings.Count == 0 ? null : [.. run.RunSettings.Select(FromWireRunSetting)],
			run.LayerIndex);

	private static WireRunSetting ToWireRunSetting(RunSetting setting) =>
		new()
		{
			Key = setting.Key,
			Kind = (int)setting.Kind,
			IntValue = setting.IntValue,
			FloatValue = setting.FloatValue,
			BoolValue = setting.BoolValue,
			StringValue = setting.StringValue,
		};

	private static RunSetting FromWireRunSetting(WireRunSetting setting) =>
		new(
			setting.Key,
			(RunSettingKind)setting.Kind,
			setting.IntValue,
			setting.FloatValue,
			setting.BoolValue,
			setting.StringValue);

	public static WireEntityPosition ToWireEntityPosition(EntityPosition position) =>
		new()
		{
			X = position.X,
			Y = position.Y,
		};

	public static EntityPosition FromWireEntityPosition(WireEntityPosition position) =>
		new(position.X, position.Y);

	public static WireWorldEntityState ToWireWorldEntityState(WorldEntityState? state) =>
		new()
		{
			Consumptions = [.. (state?.Consumptions ?? []).Select(c => new WireTrapConsumption
			{
				Position = ToWireEntityPosition(c.Position),
				Kind = c.Kind,
				Extra = c.Extra,
				TriggeredAtMs = c.TriggeredAtMs,
			})],
			BuildingHealth = [.. (state?.BuildingHealth ?? []).Select(h => new WireBuildingEntityHealth
			{
				Position = ToWireEntityPosition(h.Position),
				Health = h.Health,
			})],
			OpenedEntities = [.. (state?.OpenedEntities ?? []).Select(o => new WireOpenedEntity
			{
				Position = ToWireEntityPosition(o.Position),
			})],
			TrapStates = [.. (state?.TrapStates ?? []).Select(s => new WireTrapState
			{
				Position = ToWireEntityPosition(s.Position),
				Kind = s.Kind,
				Phase = (int)s.Phase,
				Extra = s.Extra,
				TransitionedAtMs = s.TransitionedAtMs,
			})],
		};

	public static WorldEntityState FromWireWorldEntityState(WireWorldEntityState? state)
	{
		if (state is null)
		{
			return WorldEntityState.Empty;
		}

		return new WorldEntityState(
			[.. state.Consumptions.Select(c => new TrapConsumptionFact(
				FromWireEntityPosition(c.Position),
				c.Kind,
				c.Extra,
				c.TriggeredAtMs))],
			[.. state.BuildingHealth.Select(h => new BuildingEntityHealthFact(
				FromWireEntityPosition(h.Position),
				h.Health))],
			[.. state.OpenedEntities.Select(o => new OpenedEntityFact(
				FromWireEntityPosition(o.Position)))],
			[.. state.TrapStates.Select(s => new TrapStateFact(
				FromWireEntityPosition(s.Position),
				s.Kind,
				(TrapPhase)s.Phase,
				s.Extra,
				s.TransitionedAtMs))]);
	}

	public static WirePlayerState ToWirePlayerState(PlayerState state) =>
		new()
		{
			SteamId = state.SteamId,
			Alive = state.Alive,
			Conscious = state.Conscious,
			CarrierOfSteamId = state.CarrierOfSteamId ?? 0,
			CarriedBySteamId = state.CarriedBySteamId ?? 0,
			Limbs = [.. (state.Limbs ?? []).Select(ToWirePlayerLimbState)],
			Body = state.Body is null ? null : ToWirePlayerBodyTerminalState(state.Body),
		};

	public static PlayerState FromWirePlayerState(WirePlayerState state) =>
		new(
			state.SteamId,
			state.Alive,
			state.Conscious,
			state.CarrierOfSteamId == 0 ? null : state.CarrierOfSteamId,
			state.CarriedBySteamId == 0 ? null : state.CarriedBySteamId,
			state.Limbs.Count == 0 ? null : [.. state.Limbs.Select(FromWirePlayerLimbState)],
			state.Body is null ? null : FromWirePlayerBodyTerminalState(state.Body));

	public static WirePlayerLimbState ToWirePlayerLimbState(PlayerLimbState limb) =>
		new()
		{
			Index = limb.Index,
			Broken = limb.Broken,
			Dismembered = limb.Dismembered,
			Dislocated = limb.Dislocated,
			Splinted = limb.Splinted,
			Infected = limb.Infected,
			BlockedBleeding = limb.BlockedBleeding,
			IsHead = limb.IsHead,
			IsVital = limb.IsVital,
		};

	public static PlayerLimbState FromWirePlayerLimbState(WirePlayerLimbState limb) =>
		new(
			limb.Index,
			limb.Broken,
			limb.Dismembered,
			limb.Dislocated,
			limb.Splinted,
			limb.Infected,
			limb.BlockedBleeding,
			limb.IsHead,
			limb.IsVital);

	public static WirePlayerBodyTerminalState ToWirePlayerBodyTerminalState(PlayerBodyTerminalState body) =>
		new()
		{
			Disfigured = body.Disfigured,
			EyeGone = body.EyeGone,
			BothEyesGone = body.BothEyesGone,
			HasPulmonaryEmbolism = body.HasPulmonaryEmbolism,
			TriedRollingLastStand = body.TriedRollingLastStand,
			SuccesfullyRolledLastStand = body.SuccesfullyRolledLastStand,
			UsedNeuralBooster = body.UsedNeuralBooster,
			FibrillationForced = body.FibrillationForced,
			MindwipeScriptPresent = body.MindwipeScriptPresent,
			MindwipeScriptActive = body.MindwipeScriptActive,
		};

	public static PlayerBodyTerminalState FromWirePlayerBodyTerminalState(WirePlayerBodyTerminalState body) =>
		new(
			body.Disfigured,
			body.EyeGone,
			body.BothEyesGone,
			body.HasPulmonaryEmbolism,
			body.TriedRollingLastStand,
			body.SuccesfullyRolledLastStand,
			body.UsedNeuralBooster,
			body.FibrillationForced,
			body.MindwipeScriptPresent,
			body.MindwipeScriptActive);

	public static WireEntityId ToWireEntityId(EntityId id) =>
		new()
		{
			Epoch = id.Epoch,
			Counter = id.Counter,
			Generation = id.Generation,
		};

	public static EntityId FromWireEntityId(WireEntityId id) =>
		new(id.Epoch, id.Counter, id.Generation);

	public static WireEnemyState ToWireEnemyState(EnemyState state) =>
		new()
		{
			EntityId = ToWireEntityId(state.EntityId),
			PrefabId = state.PrefabId,
			Health = state.Health,
			RuntimeSpawned = state.RuntimeSpawned,
			Stunned = state.Stunned,
		};

	public static EnemyState FromWireEnemyState(WireEnemyState state) =>
		new(
			FromWireEntityId(state.EntityId),
			state.PrefabId,
			state.Health,
			state.RuntimeSpawned,
			state.Stunned);

	public static IReadOnlyList<WireEntityId> ToWireRemovedEnemyIds(IEnumerable<EntityId> removed) =>
		[.. removed.Select(ToWireEntityId)];

	public static IReadOnlyList<EntityId> FromWireRemovedEnemyIds(IEnumerable<WireEntityId> removed) =>
		[.. removed.Select(FromWireEntityId)];

	public static WireEvent ToWireTrapStateEvent(TrapStateChangedEvent state) =>
		new()
		{
			Kind = WireEventKind.TrapStateChanged,
			EntityPosition = ToWireEntityPosition(state.Position),
			EntityKind = state.Kind,
			Extra = state.Extra,
			TriggeredAtMs = state.TransitionedAtMs,
			TrapPhase = (int)state.Phase,
		};

	public static TrapStateChangedEvent FromWireTrapStateEvent(WireEvent @event) =>
		new(
			FromWireEntityPosition(@event.EntityPosition ?? throw new System.InvalidOperationException("TrapStateChanged event lacks position")),
			@event.EntityKind,
			(TrapPhase)@event.TrapPhase,
			@event.Extra,
			@event.TriggeredAtMs);

	public static RecordTrapStateCommand FromWireRecordTrapStateCommand(
		WireCommand command,
		OperationId operation,
		ActorId actor,
		RunEpoch epoch,
		AuthorityKind authority) =>
		new(
			operation,
			actor,
			epoch,
			authority,
			FromWireEntityPosition(command.EntityPosition ?? throw new System.InvalidOperationException("RecordTrapState command lacks position")),
			command.EntityKind,
			(TrapPhase)command.TrapPhase,
			command.Extra,
			command.TriggeredAtMs);

	public static WireFluidRegionState ToWireFluidRegionState(FluidRegionState state) =>
		new()
		{
			ChunkX = state.ChunkX,
			ChunkY = state.ChunkY,
			TotalAmount = state.TotalAmount,
			MainType = state.MainType,
			UpdatedAtMs = state.UpdatedAtMs,
		};

	public static FluidRegionState FromWireFluidRegionState(WireFluidRegionState state) =>
		new(
			state.ChunkX,
			state.ChunkY,
			state.TotalAmount,
			state.MainType,
			state.UpdatedAtMs);
}
