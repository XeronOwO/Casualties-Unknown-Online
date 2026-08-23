using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The player-character attack-animation presentation chain: the source side's
/// <c>Body.Attack</c> instantiated the <c>attackAnim</c> prefab locally; the
/// exact prefab name + facing + attack direction travel as one dedicated
/// reliable <see cref="CharacterAttackAnimMsg"/>, and every receiving side
/// replays the same visual on the owner's render clone. The receiver's replay
/// runs inside a RemoteApply scope so any capture patches cannot echo the
/// replay. No state is held across calls; a lost message is a lost one-shot
/// presentation (acceptable degradation — there is no persistent fact to heal).
/// </summary>
internal sealed class CharacterAttackAnimSync(
	ICharacterDataControl characterData,
	ISessionControl session,
	RemotePlayerRenderer renderer,
	ILogger<CharacterAttackAnimSync> log)
{
	private readonly ICharacterDataControl _characterData = characterData;
	private readonly ISessionControl _session = session;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<CharacterAttackAnimSync> _log = log;

	internal void BindToSession() => _characterData.CharacterAttackAnimReceived += OnReceived;

	internal void Unbind() => _characterData.CharacterAttackAnimReceived -= OnReceived;

	/// <summary>
	/// The Body.Attack postfix verified the local attack will run (conscious +
	/// off-cooldown + doAttackAnim) and the prefab is non-null. Report the
	/// one-shot: a guest sends to the host; the host broadcasts to every
	/// handshaken guest (it already saw its own animation locally).
	/// </summary>
	internal void Report(string prefab, Vector2 direction, bool isRight, Vector2 position)
	{
		if (!_session.SessionActive || string.IsNullOrEmpty(prefab))
		{
			return;
		}

		_characterData.SendCharacterAttackAnim(new CharacterAttackAnimMsg
		{
			OwnerSteamId = _session.LocalSteamId,
			Prefab = prefab,
			Position = new NetVector2Msg { X = position.x, Y = position.y },
			Direction = new NetVector2Msg { X = direction.x, Y = direction.y },
			IsRight = isRight,
		});
	}

	/// <summary>
	/// A report (host) or relay (guest) arrived — replay the exact attack-anim
	/// prefab on the owner's clone. When the clone exists the visual is
	/// parented to it and anchored at the clone's live arm; otherwise the
	/// reported world position is still used so the one-shot is not silently
	/// lost during a clone-creation race (a world-entry edge).
	/// </summary>
	private void OnReceived(ulong sender, CharacterAttackAnimMsg msg)
	{
		if (msg.OwnerSteamId == _session.LocalSteamId)
		{
			_log.LogWarning("[CharacterAttackAnim] own animation echo from {Sender} dropped.", sender);
			return;
		}

		var direction = new Vector2(msg.Direction.X, msg.Direction.Y);
		if (direction.sqrMagnitude < 0.0001f)
		{
			_log.LogWarning("[CharacterAttackAnim] {Owner} reported a zero attack direction — dropped.", msg.OwnerSteamId);
			return;
		}

		var prefab = Resources.Load<GameObject>(msg.Prefab);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("[CharacterAttackAnim] attack-anim prefab {Prefab} not found — dropped.", msg.Prefab);
			return;
		}

		var hasBody = _renderer.TryGetRemoteBody(msg.OwnerSteamId, out var body);
		var pos = hasBody && body != null
			? (Vector2)body.limbs[1].transform.position
			: new Vector2(msg.Position.X, msg.Position.Y);
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var go = Object.Instantiate(prefab);
			go.transform.position = pos;
			go.transform.eulerAngles = new Vector3(0f, 0f,
				Vector2.SignedAngle(msg.IsRight ? Vector3.right : Vector3.left, direction));
			go.transform.localScale = new Vector3(msg.IsRight ? 1f : -1f, 1f, 1f);
			if (hasBody && body != null) // Unity object — ==
			{
				go.transform.SetParent(body.transform);
			}

			Object.Destroy(go, 5f);
		}

		_log.LogDebug("[CharacterAttackAnim] replayed {Prefab} for owner {Owner} at ({X:F1},{Y:F1}).",
			msg.Prefab, msg.OwnerSteamId, pos.x, pos.y);
	}
}
