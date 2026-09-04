using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-side command authority for journal-only player-interaction result
/// events. It uses the shared <see cref="ItemKernelAuthority"/> kernel/operation
/// counter without expanding that class past the architecture line gate.
/// </summary>
internal sealed class PlayerInteractionResultAuthority(ItemKernelAuthority kernelAuthority)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;

	public bool TryRecordPlayerInventoryTransfer(
		ulong actor,
		ulong fromSteamId,
		ulong toSteamId,
		PlayerInteractionItem item,
		ulong targetParentItemId,
		out CommittedBatch? batch,
		out Rejection? rejection) =>
		TryExecute(
			new RecordPlayerInventoryTransferCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(actor),
				_kernelAuthority.CurrentRunEpoch,
				PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthorityPolicy.Take),
				fromSteamId,
				toSteamId,
				item,
				targetParentItemId),
			actor,
			"record-player-inventory-transfer",
			out batch,
			out rejection);

	public bool TryRecordPlayerHealResult(
		ulong actor,
		ulong healerSteamId,
		ulong targetSteamId,
		ulong itemInstanceId,
		bool itemDestroyed,
		float itemConditionAfter,
		int healedLimbIndex,
		PlayerInteractionHealth? health,
		IReadOnlyList<PlayerInteractionLimb> limbs,
		out CommittedBatch? batch,
		out Rejection? rejection) =>
		TryExecute(
			new RecordPlayerHealResultCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(actor),
				_kernelAuthority.CurrentRunEpoch,
				PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthorityPolicy.Heal),
				healerSteamId,
				targetSteamId,
				itemInstanceId,
				itemDestroyed,
				itemConditionAfter,
				healedLimbIndex,
				health,
				limbs),
			actor,
			"record-player-heal-result",
			out batch,
			out rejection);

	public bool TryRecordPlayerItemUseResult(
		ulong actor,
		ulong userSteamId,
		ulong targetSteamId,
		ulong itemInstanceId,
		bool itemDestroyed,
		PlayerInteractionItem? itemAfter,
		PlayerInteractionItem? wornItem,
		PlayerInteractionHealth? health,
		IReadOnlyList<PlayerInteractionLimb> limbs,
		IReadOnlyList<PlayerInteractionTimedLimbEffect> timedEffects,
		IReadOnlyList<PlayerInteractionTimedBodyEffect> timedBodyEffects,
		out CommittedBatch? batch,
		out Rejection? rejection) =>
		TryExecute(
			new RecordPlayerItemUseResultCommand(
				_kernelAuthority.NextOperationId(),
				new ActorId(actor),
				_kernelAuthority.CurrentRunEpoch,
				PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthorityPolicy.Use),
				userSteamId,
				targetSteamId,
				itemInstanceId,
				itemDestroyed,
				itemAfter,
				wornItem,
				health,
				limbs,
				timedEffects,
				timedBodyEffects),
			actor,
			"record-player-item-use-result",
			out batch,
			out rejection);

	private bool TryExecute(
		GameCommand command,
		ulong actor,
		string label,
		out CommittedBatch? batch,
		out Rejection? rejection) =>
		_kernelAuthority.TryExecuteHostCommand(command, actor, label, out batch, out rejection);
}
