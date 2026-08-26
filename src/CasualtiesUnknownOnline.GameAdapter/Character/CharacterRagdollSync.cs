using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The player-character ragdoll-toggle presentation chain: the source side's
/// local <c>Body.Ragdoll</c> already collapsed the body; the one-shot travels
/// as one dedicated reliable <see cref="CharacterRagdollMsg"/>, and every
/// receiving side immediately replays the lying pose on the owner's render
/// clone. The receiver's replay runs inside a RemoteApply scope so any capture
/// patches cannot echo the replay. The 20 Hz entity-state stream remains the
/// fallback for the continuous standing flag; the event only makes the trigger
/// visible without waiting for (or losing to) that unreliable stream.
/// </summary>
internal sealed class CharacterRagdollSync(
	ICharacterDataControl characterData,
	ISessionControl session,
	RemotePlayerRenderer renderer,
	ILogger<CharacterRagdollSync> log)
{
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly ISessionControl _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<CharacterRagdollSync> _log = log;

	internal void BindToSession() => _characterData.CharacterRagdollReceived += OnReceived;

	internal void Unbind() => _characterData.CharacterRagdollReceived -= OnReceived;

	/// <summary>
	/// PlayerCamera.HandleInput verified the local body just transitioned from
	/// standing to collapsing via the game's ragdoll input. Report the one-shot:
	/// a guest sends to the host; the host broadcasts to every handshaken guest
	/// (it already collapsed locally).
	/// </summary>
	internal void Report(Vector2 position)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_characterData.SendCharacterRagdoll(new CharacterRagdollMsg
		{
			OwnerSteamId = _session.LocalSteamId,
			Position = new NetVector2Msg { X = position.x, Y = position.y },
		});
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the lying pose on the
	/// owner's clone. When the clone exists, the native lay-down clip pair is
	/// played directly and the clone's standing flag is forced false; the
	/// driver's PrevLying is seeded so the next state-stream snapshot does not
	/// replay the same transition. If the clone does not exist yet, the event is
	/// intentionally dropped (the 20 Hz state stream will supply the pose when
	/// the clone appears).
	/// </summary>
	private void OnReceived(ulong sender, CharacterRagdollMsg msg)
	{
		if (msg.OwnerSteamId == _session.LocalSteamId)
		{
			_log.LogWarning("[CharacterRagdoll] own ragdoll echo from {Sender} dropped.", sender);
			return;
		}

		if (!_renderer.TryGetRemoteBody(msg.OwnerSteamId, out var body) || body == null) // Unity object — ==
		{
			_log.LogDebug("[CharacterRagdoll] {Owner} clone not ready — dropped (state stream fallback).", msg.OwnerSteamId);
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			body.bodyAnimator.Play("ExperimentLayDown");
			body.armsAnimator.Play("ArmsLayDown");
			body.standing = false;
			if (body.TryGetComponent<RemoteBodyDriver>(out var driver))
			{
				driver.PrevLying = true;
			}
		}

		_log.LogDebug("[CharacterRagdoll] replayed lying pose for owner {Owner} at ({X:F1},{Y:F1}).",
			msg.OwnerSteamId, msg.Position.X, msg.Position.Y);
	}
}
