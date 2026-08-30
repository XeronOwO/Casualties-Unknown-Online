using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Host/guest entry point for enemy-combat result reporting. Host reports
/// commit journal-only kernel commands directly; guest reports ride the Phase C
/// command envelope so the host commits and every peer applies the same
/// authoritative batch. No legacy direct result wire is produced by this path.
/// </summary>
internal sealed class EnemyCombatKernelSubmitter(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority,
	IKernelProtocolControl kernelProtocol,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly ILogger _log = log;

	public void SendEnemyBite(EnemyBiteMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			RecordBite(msg);
		}
		else if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(
				new WireCommand
				{
					Kind = WireCommandKind.RecordEnemyBite,
					EnemyCombat = EnemyCombatWireMapper.ToWire(msg),
				},
				WirePayloadType.RecordEnemyBiteCommand);
		}
	}

	public void SendEnemyLunge(EnemyLungeMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			RecordLunge(msg);
		}
		else if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(
				new WireCommand
				{
					Kind = WireCommandKind.RecordEnemyLunge,
					EnemyCombat = EnemyCombatWireMapper.ToWire(msg),
				},
				WirePayloadType.RecordEnemyLungeCommand);
		}
	}

	public void SendEnemyEffect(EnemyEffectMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			RecordEffect(msg);
		}
		else if (_session.Role == SessionRole.Guest)
		{
			_kernelProtocol.SendCommand(
				new WireCommand
				{
					Kind = WireCommandKind.RecordEnemyEffect,
					EnemyCombat = EnemyCombatWireMapper.ToWire(msg),
				},
				WirePayloadType.RecordEnemyEffectCommand);
		}
	}

	private void RecordBite(EnemyBiteMsg msg)
	{
		var command = new RecordEnemyBiteCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			msg.VictimSteamId,
			EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb),
			msg.VenomTotal,
			msg.Adrenaline,
			msg.Happiness);
		Execute(command, "record-enemy-bite");
	}

	private void RecordLunge(EnemyLungeMsg msg)
	{
		var command = new RecordEnemyLungeCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			msg.VictimSteamId,
			EnemyCombatKernelCodec.FromCharacterLimb(msg.Limb),
			msg.Adrenaline,
			msg.Stamina);
		Execute(command, "record-enemy-lunge");
	}

	private void RecordEffect(EnemyEffectMsg msg)
	{
		var command = new RecordEnemyEffectCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(_session.LocalSteamId),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			msg.VictimSteamId,
			EnemyCombatKernelCodec.FromRuntimeEffectKind(msg.Kind),
			msg.HorrifiedLevel,
			msg.FocusedLevel,
			msg.Adrenaline,
			msg.Energy,
			msg.Stamina,
			msg.Happiness,
			msg.Caffeinated,
			msg.SepticShock,
			msg.Shock,
			msg.EyePanicTime);
		Execute(command, "record-enemy-effect");
	}

	private void Execute(GameCommand command, string label)
	{
		if (!_kernelAuthority.TryExecuteHostCommand(command, _session.LocalSteamId, label, out _, out var rejection))
		{
			_log.LogWarning("[EnemyCombatKernel] {Label} rejected: {Reason} ({Message}).",
				label, rejection!.Reason, rejection.Message);
		}
	}
}
