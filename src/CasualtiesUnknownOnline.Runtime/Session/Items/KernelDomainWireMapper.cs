using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
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
				FromWireEntityPosition(o.Position)))]);
	}

	public static WirePlayerState ToWirePlayerState(PlayerState state) =>
		new()
		{
			SteamId = state.SteamId,
			Alive = state.Alive,
			Conscious = state.Conscious,
		};

	public static PlayerState FromWirePlayerState(WirePlayerState state) =>
		new(state.SteamId, state.Alive, state.Conscious);

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
}
