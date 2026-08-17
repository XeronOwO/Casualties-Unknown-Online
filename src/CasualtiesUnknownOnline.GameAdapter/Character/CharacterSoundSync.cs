using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The player-character action-presentation chain (attack swing / throw
/// swing / exert / gun fire / footstep / landing impact): the source side's
/// <c>Sound.Play</c> call is captured by the patches (call-identity scope for
/// the swings/exert/footstep/landing, the GunScript.Fire postfix for gun
/// fire), the exact clip + position + volume/follow + recoil facts travel as
/// one dedicated reliable <see cref="CharacterSoundMsg"/>, and every receiving
/// side replays it on the owner's render clone. The receiver's replay runs
/// inside a RemoteApply scope, so the capture patches can never echo the
/// replay. No state is held across calls; a lost message is a lost one-shot
/// presentation (acceptable degradation — there is no persistent fact to heal).
/// </summary>
internal sealed class CharacterSoundSync(
	CharacterDataStore characterData,
	SessionService session,
	RemotePlayerRenderer renderer,
	ILogger<CharacterSoundSync> log)
{
	private readonly CharacterDataStore _characterData = characterData;
	private readonly SessionService _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<CharacterSoundSync> _log = log;

	internal void BindToSession() => _characterData.CharacterSoundReceived += OnReceived;

	internal void Unbind() => _characterData.CharacterSoundReceived -= OnReceived;

	/// <summary>
	/// The patch verified the game played a character action sound (the exact
	/// <c>Sound.Play</c> call ran — the fact is committed by construction).
	/// Report it: a guest sends to the host; the host broadcasts to every
	/// handshaken guest (it already heard its own sound).
	/// </summary>
	internal void Report(CharacterSoundKind kind, string clip, Vector2 pos, float volume,
		bool followOwner, bool twoDimensional, float recoilDegrees = 0f)
	{
		if (!_session.SessionActive || string.IsNullOrEmpty(clip))
		{
			return;
		}

		_characterData.SendCharacterSound(new CharacterSoundMsg
		{
			OwnerSteamId = _session.LocalSteamId,
			Kind = kind,
			Clip = clip,
			Position = new NetVector2Msg { X = pos.x, Y = pos.y },
			Volume = volume,
			FollowOwner = followOwner,
			TwoDimensional = twoDimensional,
			RecoilDegrees = recoilDegrees,
		});
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the exact sound on
	/// the owner's clone. FollowOwner re-parents the played sound to the
	/// remote body when it exists (the clone may not be created yet at a
	/// world-entry edge — the position fallback still plays the sound).
	/// </summary>
	private void OnReceived(ulong sender, CharacterSoundMsg msg)
	{
		if (msg.OwnerSteamId == _session.LocalSteamId)
		{
			// Star topology never sends an owner's sound back to the owner;
			// if one arrives anyway it is stale/misrouted — never double-play.
			_log.LogWarning("[CharacterSound] own sound echo from {Sender} dropped.", sender);
			return;
		}

		var pos = new Vector2(msg.Position.X, msg.Position.Y);
		var hasBody = _renderer.TryGetRemoteBody(msg.OwnerSteamId, out var body);
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.FollowOwner && hasBody && body != null) // Unity object — ==
			{
				Sound.Play(msg.Clip, pos, msg.TwoDimensional, true, body.transform, msg.Volume, 1f, false, false);
			}
			else
			{
				Sound.Play(msg.Clip, pos, msg.TwoDimensional, true, null, msg.Volume, 1f, false, false);
			}
		}

		// GunFire: the owner's Fire added knockBack * 8 to gunangle
		// (GunScript.cs:221) — mirror the same one-shot kick on the clone's
		// arms animator. Body.HandleVisuals lerps gunangle back to the synced
		// aim on the next frame (Body.cs:3271), so this is a natural transient.
		if (msg.Kind == CharacterSoundKind.GunFire && hasBody && body != null && msg.RecoilDegrees != 0f) // Unity object — ==
		{
			body.armsAnimator.SetFloat("gunangle", body.armsAnimator.GetFloat("gunangle") + msg.RecoilDegrees);
		}

		_log.LogDebug("[CharacterSound] replayed {Kind} {Clip} for owner {Owner} at ({X:F1},{Y:F1}).",
			msg.Kind, msg.Clip, msg.OwnerSteamId, pos.x, pos.y);
	}
}
