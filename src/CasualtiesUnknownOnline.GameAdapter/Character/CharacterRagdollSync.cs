using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
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
	private readonly Dictionary<ulong, PendingRagdoll> _pending = [];

	private sealed class PendingRagdoll
	{
		internal CharacterRagdollMsg Msg = null!;
		internal long ReceivedMs;
	}

	internal void BindToSession()
	{
		_characterData.CharacterRagdollReceived += OnReceived;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	internal void Unbind()
	{
		_characterData.CharacterRagdollReceived -= OnReceived;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
		_pending.Clear();
	}

	/// <summary>Session ended — drop any clone-creation-race queue; stale ragdoll events must not bleed into a future world.</summary>
	internal void Reset() => _pending.Clear();

	/// <summary>
	/// A remote left the world — its clone is gone and a queued collapse waiting
	/// for clone creation must not be applied to a later re-entry/clone. The
	/// state stream (fresh world entry) is authoritative for the new pose.
	/// </summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld || !_pending.Remove(steamId))
		{
			return;
		}

		_log.LogDebug("[CharacterRagdoll] dropped queued collapse for {Owner}: remote left the world.", steamId);
	}

	/// <summary>
	/// Drain the clone-creation race queue. Called after the remote renderer's
	/// per-frame lazy clone creation, so an event that arrived before the
	/// owner's clone existed is applied as soon as the clone appears (within
	/// the suppression window; older events are discarded — the state stream is
	/// authoritative for stale poses).
	/// </summary>
	internal void Update()
	{
		if (_pending.Count == 0)
		{
			return;
		}

		var now = Environment.TickCount;
		var remove = new List<ulong>();
		foreach (var pair in _pending)
		{
			if (now - pair.Value.ReceivedMs > RagdollPoseGate.SuppressWindowMs)
			{
				_log.LogDebug("[CharacterRagdoll] queued collapse for {Owner} expired before clone creation — state stream fallback.", pair.Key);
				remove.Add(pair.Key);
				continue;
			}

			if (TryApply(pair.Value.Msg))
			{
				remove.Add(pair.Key);
			}
		}

		foreach (var owner in remove)
		{
			_pending.Remove(owner);
		}
	}

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
	/// owner's clone. If the clone is not created yet, the event is queued for a
	/// short clone-creation window instead of being dropped outright.
	/// </summary>
	private void OnReceived(ulong sender, CharacterRagdollMsg msg)
	{
		if (msg.OwnerSteamId == _session.LocalSteamId)
		{
			_log.LogWarning("[CharacterRagdoll] own ragdoll echo from {Sender} dropped.", sender);
			return;
		}

		if (!TryApply(msg))
		{
			_pending[msg.OwnerSteamId] = new PendingRagdoll
			{
				Msg = msg,
				ReceivedMs = Environment.TickCount,
			};
			_log.LogDebug("[CharacterRagdoll] {Owner} clone not ready — queued for clone creation.", msg.OwnerSteamId);
		}
	}

	private bool TryApply(CharacterRagdollMsg msg)
	{
		if (!_renderer.TryGetRemoteBody(msg.OwnerSteamId, out var body) || body == null) // Unity object — ==
		{
			return false;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			// Capture the pre-replay pose before forcing the visual: if the
			// state stream already delivered standing=false, the collapse is
			// already confirmed and the next standing=true is a real stand-up.
			var wasStanding = body.standing;
			body.bodyAnimator.Play("ExperimentLayDown");
			body.armsAnimator.Play("ArmsLayDown");
			body.standing = false;
			if (body.TryGetComponent<RemoteBodyDriver>(out var driver))
			{
				driver.PrevLying = true;
				// The one-shot must not be overwritten by a stale standing=true
				// snapshot that is still in flight on the unreliable 20 Hz
				// stream. Keep the collapse latch armed until the stream confirms
				// standing=false or the suppression window expires.
				driver.RagdollCollapsePending = true;
				driver.RagdollCollapseConfirmed = !wasStanding;
				driver.RagdollCollapseMs = Environment.TickCount;
			}
		}

		_log.LogDebug("[CharacterRagdoll] replayed lying pose for owner {Owner} at ({X:F1},{Y:F1}).",
			msg.OwnerSteamId, msg.Position.X, msg.Position.Y);
		return true;
	}
}
