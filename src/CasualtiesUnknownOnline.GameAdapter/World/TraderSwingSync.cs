using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The hostile trader swing presentation chain: the acting side's local
/// <c>TraderScript.Swing</c> already spawned the attackAnimation prefab and
/// played the swing sound against that side's local player; the direction,
/// trader position and prefab name travel as one dedicated reliable
/// <see cref="TraderSwingMsg"/>, and every other member replays the same visual
/// on its same-position trader. The host executes the star relay; no
/// trader-state or damage is touched here (those stay in the real trade domain).
/// </summary>
internal sealed class TraderSwingSync(
	IWorldControl world,
	ISessionControl session,
	ILogger<TraderSwingSync> log)
{
	private const float PositionTolerance = 2f; // same trader position key as the trade domain

	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<TraderSwingSync> _log = log;

	internal void BindToSession() => _world.TraderSwingReceived += OnReceived;

	internal void Unbind() => _world.TraderSwingReceived -= OnReceived;

	/// <summary>
	/// The TraderScript.Swing postfix verified the native swing ran on this
	/// side. Report the presentation so the other members replay it: a guest
	/// sends to the host; the host sends to every handshaken guest (it already
	/// saw the swing locally).
	/// </summary>
	internal void Report(TraderScript trader)
	{
		if (!_session.SessionActive || trader == null) // Unity object — ==
		{
			return;
		}

		var body = PlayerCamera.main.body;
		var torso = trader.torso;
		if (body == null || torso == null) // Unity objects — ==
		{
			return;
		}

		var direction = (body.transform.position - torso.position).normalized;
		var prefab = trader.attackAnimation != null ? trader.attackAnimation.name : ""; // Unity object — ==
		_world.SendTraderSwing(new TraderSwingMsg
		{
			Position = new NetVector2Msg { X = trader.transform.position.x, Y = trader.transform.position.y },
			Direction = new NetVector2Msg { X = direction.x, Y = direction.y },
			Prefab = prefab,
		});

		_log.LogInformation("[TraderSwing] reported trader=({X:0.0},{Y:0.0}) dir=({Dx:0.00},{Dy:0.00}) prefab={Prefab}.",
			trader.transform.position.x, trader.transform.position.y, direction.x, direction.y, prefab);
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the swing on the
	/// receiver's same-position trader. The host replays a guest's report on
	/// its own trader; a guest replays the host's broadcast on its own trader.
	/// </summary>
	private void OnReceived(ulong sender, TraderSwingMsg msg)
	{
		var trader = FindTraderAt(msg.Position);
		if (trader == null) // Unity object — ==
		{
			_log.LogWarning("[TraderSwing] trader not found at ({X:0.0},{Y:0.0}) — dropped.", msg.Position.X, msg.Position.Y);
			return;
		}

		TraderSwingReplay.Play(trader, msg);
		_log.LogDebug("[TraderSwing] replayed swing at ({X:0.0},{Y:0.0}) from {Sender}.", msg.Position.X, msg.Position.Y, sender);
	}

	private static TraderScript? FindTraderAt(NetVector2Msg position)
	{
		TraderScript? best = null; // Unity object — ==
		var bestDistance = float.MaxValue;
		foreach (var trader in Object.FindObjectsOfType<TraderScript>())
		{
			var distance = Vector2.Distance(trader.transform.position, new Vector2(position.X, position.Y));
			if (distance <= PositionTolerance && distance < bestDistance)
			{
				best = trader;
				bestDistance = distance;
			}
		}

		return best;
	}
}
