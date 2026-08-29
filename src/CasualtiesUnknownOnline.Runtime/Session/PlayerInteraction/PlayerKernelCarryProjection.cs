using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Projects host-authoritative carry kernel facts into the local
/// <see cref="PlayerCarryService"/> mirror. Host mutations are applied through
/// <see cref="ItemKernelAuthority.BatchCommitted"/>; guest received batches
/// through <see cref="ItemKernelAuthority.BatchApplied"/>; checkpoint restore
/// rebuilds the mirror from the kernel player table. This is the single carry
/// projection path — no legacy carry-state wire remains.
/// </summary>
internal sealed class PlayerKernelCarryProjection : IDisposable
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly PlayerCarryService _carry;
	private readonly ISessionControl _session;
	private readonly ILogger _log;

	public PlayerKernelCarryProjection(
		ItemKernelAuthority kernelAuthority,
		PlayerCarryService carry,
		ISessionControl session,
		ILogger log)
	{
		_kernelAuthority = kernelAuthority;
		_carry = carry;
		_session = session;
		_log = log;
		_kernelAuthority.BatchCommitted += OnBatchCommitted;
		_kernelAuthority.BatchApplied += OnBatchApplied;
		_kernelAuthority.CheckpointRestored += OnCheckpointRestored;
	}

	public void Dispose()
	{
		_kernelAuthority.BatchCommitted -= OnBatchCommitted;
		_kernelAuthority.BatchApplied -= OnBatchApplied;
		_kernelAuthority.CheckpointRestored -= OnCheckpointRestored;
	}

	private void OnBatchCommitted(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Host)
		{
			ApplyCarryBatch(batch);
		}
	}

	private void OnBatchApplied(CommittedBatch batch)
	{
		if (_session.Role == SessionRole.Guest)
		{
			ApplyCarryBatch(batch);
		}
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint)
	{
		_carry.RebuildFromCheckpoint(checkpoint.Players);
		_log.LogInformation("[CarryKernel] rebuilt carry mirror from checkpoint at revision {Revision}.",
			checkpoint.GlobalRevision);
	}

	private void ApplyCarryBatch(CommittedBatch batch)
	{
		foreach (var @event in batch.Events)
		{
			switch (@event)
			{
				case PlayerCarrySetEvent set:
					_carry.ApplyCommittedCarry(set.CarrierSteamId, set.CarriedSteamId);
					_log.LogDebug("[CarryKernel] projected carry set {Carrier} -> {Carried}.",
						set.CarrierSteamId, set.CarriedSteamId);
					break;
				case PlayerCarryClearedEvent clear:
					_carry.ApplyCommittedCarry(clear.CarrierSteamId, 0);
					_log.LogDebug("[CarryKernel] projected carry clear {Carrier}.", clear.CarrierSteamId);
					break;
				case PlayersResetEvent:
					_carry.ResetCarryMirror();
					_log.LogDebug("[CarryKernel] projected players reset; carry mirror cleared.");
					break;
			}
		}
	}
}
