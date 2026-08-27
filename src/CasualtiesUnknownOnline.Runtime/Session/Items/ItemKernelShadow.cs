using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase A shadow observer. It feeds accepted item facts into the deterministic
/// kernel beside the legacy item path and logs rejections/divergences. It never
/// changes old authoritative state, sends wire messages, or owns production
/// facts.
/// </summary>
public sealed class ItemKernelShadow(ILogger<ItemKernelShadow> log)
{
	private readonly ILogger<ItemKernelShadow> _log = log;
	private GameStateKernel _kernel = new(new RunEpoch(1));
	private RunEpoch _runEpoch = new(1);
	private ulong _nextOperation = 1;

	/// <summary>Test/read-only window into the shadow kernel state.</summary>
	internal GameStateKernel KernelForDiagnostics => _kernel;

	/// <summary>Start a fresh shadow epoch after a session/run reset.</summary>
	public void ResetForSession()
	{
		_runEpoch = new RunEpoch(_runEpoch.Value + 1);
		_kernel = new GameStateKernel(_runEpoch);
		_nextOperation = 1;
	}

	public void ObserveSpawn(ulong actor, ulong itemId, string definitionId, float x, float y) =>
		ObserveSpawn(actor, itemId, definitionId, ItemLocation.World(x, y));

	public void ObserveSpawn(ulong actor, ulong itemId, string definitionId, ItemLocation location)
	{
		if (_kernel.FindItem(itemId) is not null)
		{
			return;
		}

		var command = new SpawnItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.TriggerObservedHostCommitted,
			new ItemIdentity(itemId, definitionId),
			location,
			0);
		TryExecute(command, actor, "spawn");
	}

	public void ObserveCarriedSpawn(ulong actor, ulong itemId, string definitionId) =>
		ObserveSpawn(actor, itemId, definitionId, ItemLocation.Carried(new ActorId(actor)));

	public void ObservePickup(ulong actor, ulong itemId)
	{
		var item = _kernel.FindItem(itemId);
		if (item is null)
		{
			_log.LogDebug("Item kernel shadow pickup skipped: {ItemId} unknown", itemId);
			return;
		}

		if (item.Value.Location.Kind == ItemLocationKind.Carried)
		{
			return;
		}

		var command = new PickUpItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			new ActorId(actor),
			item.Value.Revision);
		TryExecute(command, actor, "pickup");
	}

	public void ObserveDrop(ulong actor, ulong itemId, float x, float y, ulong parentItemId)
	{
		var item = _kernel.FindItem(itemId);
		if (item is null)
		{
			_log.LogDebug("Item kernel shadow drop skipped: {ItemId} unknown", itemId);
			return;
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return;
		}

		var command = new DropItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			ItemLocation.World(x, y, parentItemId),
			item.Value.Revision);
		TryExecute(command, actor, "drop");
	}

	public void ObserveDestroy(ulong actor, ulong itemId)
	{
		var item = _kernel.FindItem(itemId);
		if (item is null || item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return;
		}

		var command = new DestroyItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			itemId,
			TerminalKind.Destroyed,
			item.Value.Revision);
		TryExecute(command, actor, "destroy");
	}

	private OperationId NextOperation() => new(_nextOperation++);

	private void TryExecute(GameCommand command, ulong actor, string label)
	{
		var decision = _kernel.Execute(command, new CommandContext(_runEpoch, new ActorId(actor)));
		if (!decision.IsAccepted)
		{
			_log.LogWarning("Item kernel shadow {Label} rejected: {Reason} ({Message})",
				label, decision.Rejection!.Reason, decision.Rejection.Message);
		}
	}
}
