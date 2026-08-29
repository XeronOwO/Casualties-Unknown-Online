using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player push/shove operation. The host is the authority: it
/// validates that the pusher is conscious/alive/standing, both players are
/// in-world and not in a carry relation, the distance is within reach and the
/// pusher is not on cooldown. It computes the same strength formula as
/// KrokMP's push from the pusher's Strength skill, then broadcasts one committed
/// result so every side applies the correct sound/ragdoll locally. The target's
/// own client owns its body physics; the resulting motion rides the existing
/// 20 Hz player state stream as the presentation fallback.
///
/// Push is intentionally NOT a kernel fact: it is a transient presentation
/// effect. There is no durable ownership/health/relation change to commit, so
/// no kernel command/event is created and the result remains a direct
/// host→all presentation message.
/// </summary>
internal sealed class PlayerPushService : IDisposable
{
	/// <summary>KrokMP's interaction reach (9 world units) × the push server check (1.2).</summary>
	private const float MaxPushDistance = 9f * 1.2f;

	private const float MaxPushDistanceSq = MaxPushDistance * MaxPushDistance;

	/// <summary>KrokMP server-side per-pusher push cooldown (seconds).</summary>
	private const long PushCooldownMs = 1000;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly PlayerCharacterAccess _characters;
	private readonly IEntitySyncControl _entities;
	private readonly PlayerCarryService _carry;
	private readonly ITimeSource _time;
	private readonly IPlayerInteractionVisibility _visibility;
	private readonly ILogger _log;

	private readonly Dictionary<ulong, long> _lastPushMs = [];

	/// <summary>An authoritative push result arrived — the Game Adapter applies the local participant half and plays the push sound.</summary>
	public event Action<PlayerPushResultMsg>? PushReceived;

	public PlayerPushService(
		ISessionControl session,
		PacketSender sender,
		PlayerCharacterAccess characters,
		IEntitySyncControl entities,
		PlayerCarryService carry,
		ITimeSource time,
		IPlayerInteractionVisibility visibility,
		ILogger log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_entities = entities;
		_carry = carry;
		_time = time;
		_visibility = visibility;
		_log = log;

		_session.SessionEnded += OnSessionEnded;
	}

	/// <summary>Online UI entry: the local player pushes another in-world player (guest → host on the wire; host handles locally).</summary>
	public void SendPushRequest(ulong targetSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerPushRequestMsg { TargetSteamId = targetSteamId };
		if (_session.Role == SessionRole.Host)
		{
			HandlePushRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerPushRequest, msg);
		}
	}

	/// <summary>Host only: a push request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var pusher = sender;
		var target = msg.TargetSteamId;
		if (pusher == target || pusher == 0 || target == 0)
		{
			return;
		}

		if (!_characters.IsInWorld(pusher) || !_characters.IsInWorld(target))
		{
			_log.LogWarning("[Push] refused: {Pusher} or {Target} is not in-world.", pusher, target);
			return;
		}

		if (!_visibility.HasLineOfSight(pusher, target))
		{
			_log.LogInformation("[Push] refused: {Pusher} cannot see {Target}.", pusher, target);
			return;
		}

		if (InCarryRelation(pusher) || InCarryRelation(target))
		{
			_log.LogInformation("[Push] refused: {Pusher} or {Target} is in a carry/piggyback relation.", pusher, target);
			return;
		}

		var pusherData = _characters.GetCharacterData(pusher);
		if (pusherData?.Health is not { } pusherHealth || !pusherHealth.Conscious || !pusherHealth.Alive)
		{
			_log.LogInformation("[Push] refused: {Pusher} is not conscious/alive and cannot push.", pusher);
			return;
		}

		var pusherEntity = GetEntity(pusher);
		var targetEntity = GetEntity(target);
		if (pusherEntity is null || targetEntity is null)
		{
			_log.LogWarning("[Push] refused: no entity state for {Pusher}/{Target}.", pusher, target);
			return;
		}

		if (!pusherEntity.Standing)
		{
			_log.LogInformation("[Push] refused: {Pusher} is not standing.", pusher);
			return;
		}

		var dx = targetEntity.Position.X - pusherEntity.Position.X;
		var dy = targetEntity.Position.Y - pusherEntity.Position.Y;
		var distSq = (dx * dx) + (dy * dy);
		if (distSq <= 0.0001f || distSq > MaxPushDistanceSq)
		{
			_log.LogInformation("[Push] refused: {Pusher} → {Target} is out of reach (distance {Distance:F2}).", pusher, target, Math.Sqrt(distSq));
			return;
		}

		var nowMs = _time.NowMs;
		if (_lastPushMs.TryGetValue(pusher, out var lastPushMs) && nowMs - lastPushMs < PushCooldownMs)
		{
			_log.LogInformation("[Push] refused: {Pusher} is on push cooldown.", pusher);
			return;
		}

		_lastPushMs[pusher] = nowMs;
		var strength = ComputeStrength(pusherData.Skills?.Strength ?? 10);
		var inverseDistance = 1f / (float)Math.Sqrt(distSq);
		var result = new PlayerPushResultMsg
		{
			PusherSteamId = pusher,
			TargetSteamId = target,
			ForceX = dx * inverseDistance * strength,
			ForceY = dy * inverseDistance * strength,
		};

		_log.LogInformation(
			"[Push] {Pusher} pushes {Target} (force {ForceX:F2},{ForceY:F2}, strength {Strength:F2}).",
			pusher, target, result.ForceX, result.ForceY, strength);
		PublishPush(result);
	}

	/// <summary>Wire handler path: a push result arrived — surface it for the Game Adapter and UI.</summary>
	public void FirePushReceived(PlayerPushResultMsg msg) => PushReceived?.Invoke(msg);

	private void PublishPush(PlayerPushResultMsg msg)
	{
		// The host applies its own side locally; every guest receives the same
		// authoritative result (including the requesting pusher and the target).
		PushReceived?.Invoke(msg);
		_sender.SendToAll(_session.Members
			.Where(m => m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId), NetMsg.PlayerPushResult, msg);
	}

	private PlayerEntity? GetEntity(ulong steamId) =>
		steamId == _session.LocalSteamId ? _entities.LocalPlayer : _entities.GetRemotePlayer(steamId);

	private bool InCarryRelation(ulong steamId) =>
		_carry.TryGetCarrier(steamId, out _) || _carry.TryGetCarried(steamId, out _);

	private static float ComputeStrength(int strength)
	{
		var scaled = 1f + (strength - 10) * 0.1f;
		return 15f * Math.Max(0.2f, Math.Min(3f, scaled));
	}

	private void OnSessionEnded() => _lastPushMs.Clear();

	public void Dispose() => _session.SessionEnded -= OnSessionEnded;
}
