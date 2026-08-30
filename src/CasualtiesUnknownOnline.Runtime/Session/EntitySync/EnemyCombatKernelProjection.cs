using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Projects typed enemy-combat result kernel events into the
/// <see cref="EnemySyncService"/> presentation events. Host operations are
/// raised from <see cref="ItemKernelAuthority.BatchCommitted"/>; guest
/// replay/restore is raised from <see cref="ItemKernelAuthority.BatchApplied"/>.
/// No legacy direct result wire remains.
/// </summary>
internal sealed class EnemyCombatKernelProjection : IDisposable
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly EnemySyncService _enemies;
	private readonly ICharacterDataControl _characters;
	private readonly ISessionControl _session;
	private readonly ILogger _log;

	public EnemyCombatKernelProjection(
		ItemKernelAuthority kernelAuthority,
		EnemySyncService enemies,
		ICharacterDataControl characters,
		ISessionControl session,
		ILogger log)
	{
		_kernelAuthority = kernelAuthority;
		_enemies = enemies;
		_characters = characters;
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
				case EnemyBiteResultEvent bite:
					ProjectBite(batch, bite);
					break;
				case EnemyLungeResultEvent lunge:
					ProjectLunge(batch, lunge);
					break;
				case EnemyEffectResultEvent effect:
					ProjectEffect(batch, effect);
					break;
			}
		}
	}

	private void ProjectBite(CommittedBatch batch, EnemyBiteResultEvent e)
	{
		var msg = EnemyCombatKernelCodec.ToBiteMessage(e);
		if (msg.VictimSteamId == _session.LocalSteamId)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_characters.ApplyEnemyBite(msg);
		}

		_enemies.FireEnemyBiteReceived(batch.Actor.Value, msg);
		_log.LogDebug("[EnemyCombatKernel] projected bite on {Victim} (actor {Actor}).",
			msg.VictimSteamId, batch.Actor.Value);
	}

	private void ProjectLunge(CommittedBatch batch, EnemyLungeResultEvent e)
	{
		var msg = EnemyCombatKernelCodec.ToLungeMessage(e);
		if (msg.VictimSteamId == _session.LocalSteamId)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_characters.ApplyEnemyLunge(msg);
		}

		_enemies.FireEnemyLungeReceived(batch.Actor.Value, msg);
		_log.LogDebug("[EnemyCombatKernel] projected lunge on {Victim} (actor {Actor}).",
			msg.VictimSteamId, batch.Actor.Value);
	}

	private void ProjectEffect(CommittedBatch batch, EnemyEffectResultEvent e)
	{
		var msg = EnemyCombatKernelCodec.ToEffectMessage(e);
		if (msg.VictimSteamId == _session.LocalSteamId)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_characters.ApplyEnemyEffect(msg);
		}

		_enemies.FireEnemyEffectReceived(batch.Actor.Value, msg);
		_log.LogDebug("[EnemyCombatKernel] projected effect {Kind} on {Victim} (actor {Actor}).",
			msg.Kind, msg.VictimSteamId, batch.Actor.Value);
	}
}
