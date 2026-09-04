using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The local co-op location-ping domain: a bounded one-marker-per-player
/// transient buffer plus the middle-click double-click rule. It receives
/// pings through the world channel, places local pings, expires them, and
/// keeps no persistent state. The wire plumbing is the
/// <see cref="LocationPingChannel"/>; the Unity presentation is the Online UI.
/// </summary>
public sealed class LocationPingService : ILocationPingControl, IDisposable
{
	/// <summary>How long a ping remains visible after placement.</summary>
	public const long LifetimeMs = 5_000;

	/// <summary>Maximum time after a circle ping in which the next middle click upgrades it to an exclamation.</summary>
	public const long DoubleClickWindowMs = 400;

	private readonly ISessionControl _session;
	private readonly IWorldControl _world;
	private readonly ITimeSource _time;
	private readonly ILogger<LocationPingService> _log;
	private readonly Dictionary<ulong, LocationPing> _active = [];

	public LocationPingService(
		ISessionControl session,
		IWorldControl world,
		ITimeSource time,
		ILogger<LocationPingService> log)
	{
		_session = session;
		_world = world;
		_time = time;
		_log = log;
		_world.LocationPingReceived += OnLocationPingReceived;
		_session.SessionEnded += Clear;
	}

	public IReadOnlyList<LocationPing> ActivePings
	{
		get
		{
			Prune();
			return [.. _active.Values.OrderBy(p => p.SenderSteamId)];
		}
	}

	public bool TryPlace(float x, float y)
	{
		if (!_session.SessionActive || !_session.LocalInWorld || _session.Role == SessionRole.None)
		{
			return false;
		}

		var now = _time.NowMs;
		var local = _session.LocalSteamId;

		var kind = LocationPingKind.Circle;
		if (_active.TryGetValue(local, out var current)
			&& current.Kind == LocationPingKind.Circle
			&& now - current.PlacedAtMs <= DoubleClickWindowMs)
		{
			kind = LocationPingKind.Exclamation;
		}

		_active[local] = new LocationPing(local, x, y, kind, now, now + LifetimeMs);
		_world.SendLocationPing(new LocationPingMsg
		{
			SenderSteamId = local,
			Position = new NetVector2Msg(x, y),
			Kind = kind,
		});

		_log.LogDebug("[LocationPing] local {Kind} at ({X:F1},{Y:F1}).", kind, x, y);
		return true;
	}

	public void Prune()
	{
		var now = _time.NowMs;
		var expired = _active
			.Where(pair => pair.Value.ExpiresAtMs <= now)
			.Select(pair => pair.Key)
			.ToArray();
		foreach (var steamId in expired)
		{
			_active.Remove(steamId);
		}

		if (expired.Length > 0)
		{
			_log.LogDebug("[LocationPing] pruned {Count} expired ping(s).", expired.Length);
		}
	}

	private void OnLocationPingReceived(ulong sender, LocationPingMsg msg)
	{
		if (msg.SenderSteamId == _session.LocalSteamId)
		{
			_log.LogWarning("[LocationPing] own ping echo from {Sender} dropped.", sender);
			return;
		}

		if (_session.Role == SessionRole.Host && sender != msg.SenderSteamId)
		{
			_log.LogWarning("[LocationPing] spoofed sender {Claimed} from transport {Sender} dropped.", msg.SenderSteamId, sender);
			return;
		}

		if (!Enum.IsDefined(typeof(LocationPingKind), msg.Kind))
		{
			_log.LogWarning("[LocationPing] invalid kind {Kind} from {Sender} dropped.", msg.Kind, sender);
			return;
		}

		var now = _time.NowMs;
		_active[msg.SenderSteamId] = new LocationPing(
			msg.SenderSteamId,
			msg.Position.X,
			msg.Position.Y,
			msg.Kind,
			now,
			now + LifetimeMs);

		_log.LogDebug("[LocationPing] received {Kind} from owner {Owner} at ({X:F1},{Y:F1}).",
			msg.Kind, msg.SenderSteamId, msg.Position.X, msg.Position.Y);
	}

	private void Clear()
	{
		if (_active.Count > 0)
		{
			_log.LogDebug("[LocationPing] cleared {Count} ping(s) on session end.", _active.Count);
			_active.Clear();
		}
	}

	public void Dispose()
	{
		_world.LocationPingReceived -= OnLocationPingReceived;
		_session.SessionEnded -= Clear;
	}
}
