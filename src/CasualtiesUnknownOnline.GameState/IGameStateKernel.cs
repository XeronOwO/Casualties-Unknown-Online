using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

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
}
