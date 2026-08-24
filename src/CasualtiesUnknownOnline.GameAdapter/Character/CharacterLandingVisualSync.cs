using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The player-character landing presentation chain: the source side's
/// <c>Body.HandleGroundedState</c> already played the <c>Grounded</c> clip and,
/// on a hard fall, spawned the native landing dust; the exact cloud size,
/// anchor position and horizontal emitter velocity travel as one dedicated
/// reliable <see cref="CharacterLandingVisualMsg"/>, and every receiving side
/// replays the same visual on the owner's render clone. The receiver's replay
/// runs inside a RemoteApply scope so any capture patches cannot echo the
/// replay. No state is held across calls; a lost message is a lost one-shot
/// presentation (acceptable degradation — there is no persistent fact to heal).
/// </summary>
internal sealed class CharacterLandingVisualSync(
	ICharacterDataControl characterData,
	ISessionControl session,
	RemotePlayerRenderer renderer,
	ILogger<CharacterLandingVisualSync> log)
{
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly ISessionControl _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<CharacterLandingVisualSync> _log = log;

	internal void BindToSession() => _characterData.CharacterLandingVisualReceived += OnReceived;

	internal void Unbind() => _characterData.CharacterLandingVisualReceived -= OnReceived;

	/// <summary>
	/// The Body.HandleGroundedState postfix verified the local body just landed.
	/// Report the one-shot: a guest sends to the host; the host broadcasts to
	/// every handshaken guest (it already saw its own landing locally).
	/// </summary>
	internal void Report(byte cloudSize, Vector2 position, float velocityX)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_characterData.SendCharacterLandingVisual(new CharacterLandingVisualMsg
		{
			OwnerSteamId = _session.LocalSteamId,
			CloudSize = cloudSize,
			Position = new NetVector2Msg { X = position.x, Y = position.y },
			VelocityX = velocityX,
		});
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the landing visual on
	/// the owner's clone. When the clone exists, its Body methods are used so
	/// the cloud resources/anchoring match the native path; otherwise the
	/// reported world position is still used so the one-shot is not silently
	/// lost during a clone-creation race (a world-entry edge).
	/// </summary>
	private void OnReceived(ulong sender, CharacterLandingVisualMsg msg)
	{
		if (msg.OwnerSteamId == _session.LocalSteamId)
		{
			_log.LogWarning("[CharacterLandingVisual] own landing echo from {Sender} dropped.", sender);
			return;
		}

		var hasBody = _renderer.TryGetRemoteBody(msg.OwnerSteamId, out var body);
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (hasBody && body != null) // Unity object — ==
			{
				// The native landing branch always plays the Grounded clip on
				// the body animator (Body.cs:2715). On a render clone the pose
				// state machine would otherwise only react to the grounded
				// animator flag, which is not an explicit one-shot.
				body.bodyAnimator.Play("Grounded");
			}

			ReplayCloud(msg, body, hasBody);
		}

		_log.LogDebug("[CharacterLandingVisual] replayed cloud {Cloud} for owner {Owner} at ({X:F1},{Y:F1}).",
			msg.CloudSize, msg.OwnerSteamId, msg.Position.X, msg.Position.Y);
	}

	private static void ReplayCloud(CharacterLandingVisualMsg msg, Body? body, bool hasBody)
	{
		if (msg.CloudSize == CharacterLandingVisualMsg.CloudNone)
		{
			return;
		}

		var pos = new Vector2(msg.Position.X, msg.Position.Y);
		var velocity = new Vector2(msg.VelocityX, 0f);
		if (hasBody && body != null) // Unity object — ==
		{
			if (msg.CloudSize == CharacterLandingVisualMsg.CloudBig)
			{
				body.CreateCloudBig(pos, velocity);
			}
			else
			{
				body.CreateCloudSmall(pos, velocity);
			}

			return;
		}

		// Clone-creation race: still play the visible dust at the reported
		// anchor so the one-shot is not silently dropped.
		var prefabName = msg.CloudSize == CharacterLandingVisualMsg.CloudBig ? "DustBig" : "DustSmall";
		var prefab = Resources.Load<GameObject>(prefabName);
		if (prefab == null) // Unity object — ==
		{
			return;
		}

		var go = Object.Instantiate(prefab, pos, Quaternion.identity);
		var ps = go.GetComponent<ParticleSystem>();
		if (ps != null)
		{
			var main = ps.main;
			main.emitterVelocity = velocity;
		}
	}
}
