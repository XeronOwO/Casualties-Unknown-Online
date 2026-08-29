using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Projects typed player-interaction result kernel events into the
/// <see cref="IPlayerInteractionControl"/> presentation events. Host operations
/// are raised from <see cref="ItemKernelAuthority.BatchCommitted"/>; guest
/// replay/restore is raised from <see cref="ItemKernelAuthority.BatchApplied"/>.
/// No legacy direct result wire remains.
/// </summary>
internal sealed class PlayerInteractionKernelProjection : IDisposable
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly IPlayerInteractionControl _interaction;
	private readonly ISessionControl _session;
	private readonly ILogger _log;

	public PlayerInteractionKernelProjection(
		ItemKernelAuthority kernelAuthority,
		IPlayerInteractionControl interaction,
		ISessionControl session,
		ILogger log)
	{
		_kernelAuthority = kernelAuthority;
		_interaction = interaction;
		_session = session;
		_log = log;
		_kernelAuthority.BatchCommitted += OnBatchCommitted;
		_kernelAuthority.BatchApplied += OnBatchApplied;
	}

	public void Dispose()
	{
		_kernelAuthority.BatchCommitted -= OnBatchCommitted;
		_kernelAuthority.BatchApplied -= OnBatchApplied;
	}

	private void OnBatchCommitted(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Host)
		{
			Project(batch);
		}
	}

	private void OnBatchApplied(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Guest)
		{
			Project(batch);
		}
	}

	private void Project(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			switch (@event)
			{
				case PlayerInventoryTransferEvent transfer:
					ProjectTransfer(transfer);
					break;
				case PlayerHealResultEvent heal:
					ProjectHeal(heal);
					break;
				case PlayerItemUseResultEvent use:
					ProjectUse(use);
					break;
			}
		}
	}

	private void ProjectTransfer(PlayerInventoryTransferEvent e)
	{
		var local = _session.LocalSteamId;
		if (e.FromSteamId != local && e.ToSteamId != local)
		{
			return;
		}

		_interaction.FireTransferReceived(PlayerInteractionKernelCodec.ToTransferMessage(e));
		_log.LogDebug("[PlayerInteractionKernel] projected inventory transfer {From} -> {To} (item {ItemId}).",
			e.FromSteamId, e.ToSteamId, e.Item.Identity.InstanceId);
	}

	private void ProjectHeal(PlayerHealResultEvent e)
	{
		var local = _session.LocalSteamId;
		if (e.HealerSteamId != local && e.TargetSteamId != local)
		{
			return;
		}

		_interaction.FireHealReceived(PlayerInteractionKernelCodec.ToHealMessage(e));
		_log.LogDebug("[PlayerInteractionKernel] projected heal {Healer} -> {Target} (item {ItemId}).",
			e.HealerSteamId, e.TargetSteamId, e.ItemInstanceId);
	}

	private void ProjectUse(PlayerItemUseResultEvent e)
	{
		var local = _session.LocalSteamId;
		if (e.UserSteamId != local && e.TargetSteamId != local)
		{
			return;
		}

		_interaction.FireUseReceived(PlayerInteractionKernelCodec.ToUseMessage(e));
		_log.LogDebug("[PlayerInteractionKernel] projected item use {User} -> {Target} (item {ItemId}).",
			e.UserSteamId, e.TargetSteamId, e.ItemInstanceId);
	}
}
