using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Small stable kernel surface. Domain-specific behavior is expressed with
/// typed commands, not dozens of per-domain methods.
/// </summary>
public interface IGameStateKernel
{
	Decision Execute(GameCommand command, CommandContext context);

	ApplyResult Apply(CommittedBatch batch);

	GameCheckpoint CreateCheckpoint();

	RestoreResult Restore(GameCheckpoint checkpoint);

	IReadOnlyDictionary<ulong, ItemState> QueryItems();

	ItemState? FindItem(ulong instanceId);

	RunState? QueryRun();

	WorldEntityState? QueryWorldEntities();

	PlayerStateTable? QueryPlayers();

	EnemyStateTable? QueryEnemies();

	FluidStateTable? QueryFluids();
}
