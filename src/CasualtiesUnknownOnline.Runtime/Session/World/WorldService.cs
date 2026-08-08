using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world domain (world-defining state + world-change events): owns the
/// world-start parameters captured by the host at run start and applied by
/// guests before their own world generation, and shuttles block-damage reports
/// (local compute → report → host relay). Owns no session state — it reads the
/// member roster through <see cref="ISessionControl"/> and fans out with
/// <see cref="PacketSender"/>. No pump: it only reacts to calls and messages
/// (not an ICuoService, like CharacterDataStore).
/// </summary>
public sealed class WorldService(ISessionControl session, PacketSender sender, ILogger<WorldService> log)
	: IWorldControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>World-start parameters: set by the host at run start, by the world-params handler on the guest.</summary>
	public WorldStartParams? WorldParams { get; set; }

	/// <summary>Host: a guest reported damage (apply + relay). Guest: the host broadcast it.</summary>
	public event Action<NetVector2, float>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(NetVector2 pos, float damage) =>
		BlockDamagedReceived?.Invoke(pos, damage);

	/// <summary>Host side: capture and publish world-start parameters (run start).</summary>
	public void PublishWorldParams(WorldStartParams parameters)
	{
		WorldParams = parameters; // the handshake handlers read this when acking a new member
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = parameters.ToWorldStartParamsMsg();
		var members = _session.Members.Where(m => m.Handshaken).ToList();
		foreach (var member in members)
		{
			_sender.Send(member.SteamId, NetMsg.WorldStartParams, msg);
		}

		_log.LogInformation("Published world params ({StateBytes} bytes) to {Members} members.",
			parameters.RandomState.Length, members.Count);
	}

	/// <summary>
	/// Report a locally-performed block damage (local compute): guest → host as
	/// a report (the host arbitrates and relays), host → broadcast to all synced
	/// members (the source excluded on relay — it already applied locally).
	/// </summary>
	public void SendBlockDamaged(NetVector2 worldPos, float damage)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}
}
