using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The player world-blood decal presentation chain: the owner's local
/// <c>BleedParticle</c> already spawned the ground/wall blood object when its
/// fur-blood threshold dripped; the decal position and ground/wall kind travel
/// as one dedicated reliable <see cref="WorldBloodSpawnMsg"/>, and every other
/// member replays the same transient decal on its own world. The host executes
/// the star relay; no body state or world state is touched here.
/// </summary>
internal sealed class WorldBloodSync(
	IWorldControl world,
	ISessionControl session,
	ILogger<WorldBloodSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<WorldBloodSync> _log = log;

	internal void BindToSession() => _world.WorldBloodSpawnReceived += OnReceived;

	internal void Unbind() => _world.WorldBloodSpawnReceived -= OnReceived;

	/// <summary>
	/// The BleedParticle postfix verified the native decal spawn happened on the
	/// local player's body this frame. Report the one-shot so the other members
	/// replay it: a guest sends to the host; the host sends to every handshaken
	/// guest (it already spawned the decal locally).
	/// </summary>
	internal void Report(Vector2 position, bool ground)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_world.SendWorldBloodSpawn(new WorldBloodSpawnMsg
		{
			Position = new NetVector2Msg { X = position.x, Y = position.y },
			Ground = ground,
		});

		_log.LogDebug("[WorldBloodSpawn] reported ground={Ground} at ({X:F1},{Y:F1}).", ground, position.x, position.y);
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the decal on the
	/// receiver's own world. The host replays a guest's report on its world; a
	/// guest replays the host's broadcast on its own world.
	/// </summary>
	private void OnReceived(ulong sender, WorldBloodSpawnMsg msg)
	{
		WorldBloodReplay.Play(msg);
		_log.LogDebug("[WorldBloodSpawn] replayed ground={Ground} at ({X:F1},{Y:F1}) from {Sender}.",
			msg.Ground, msg.Position.X, msg.Position.Y, sender);
	}
}
