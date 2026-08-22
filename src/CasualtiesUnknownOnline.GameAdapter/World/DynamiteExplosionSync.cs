using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The dynamite player-item explosion domain (Item.cs:6671-6682,
/// CustomItemBehaviour.cs:563-572): the native detonation already ran on the
/// trigger side (local compute), so this domain only reports the one-shot
/// detonation fact and applies/replays it on the other sides. The world
/// terrain/building/item consequences from a native explosion ride the
/// existing block/building/item channels; this dedicated event carries the
/// one-shot item id + position so the receivers apply the body/visual segment
/// exactly once. The host applies its own world copy inside RemoteApply (the
/// source side's own consequences already synced through the existing
/// channels), then relays source-excluded; guests replay under RemoteApply.
/// </summary>
internal sealed class DynamiteExplosionSync(
	IWorldControl world,
	ISessionControl session,
	TrapVisualReplay replay,
	ILogger<DynamiteExplosionSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly TrapVisualReplay _replay = replay;
	private readonly ILogger<DynamiteExplosionSync> _log = log;

	/// <summary>One-shot detonations already applied/replayed per item id — an item can detonate at most once, so a reliable-channel duplicate is dropped before it can double body damage.</summary>
	private readonly HashSet<ulong> _seen = [];

	internal void BindToSession() => _world.DynamiteExplosionReceived += OnRemote;

	internal void Unbind()
	{
		_world.DynamiteExplosionReceived -= OnRemote;
		_seen.Clear();
	}

	/// <summary>The literal dynamite explosion parameters (CustomItemBehaviour.DynamiteExplode, CustomItemBehaviour.cs:563-572) — shared with both apply and replay.</summary>
	internal static ExplosionParams ExplosionParams(Vector2 position) => new()
	{
		position = position,
		range = 18f,
		structuralDamage = 2000f,
	};

	/// <summary>
	/// The patch-bridge entry after the native DynamiteExplode ran: report the
	/// detonation so the host applies it to its own world and the peers replay
	/// the body/visual segment. The item id is the one-shot identity.
	/// </summary>
	internal void OnLocalExploded(ulong itemId, Vector2 position)
	{
		if (CallContext.Current == CallContext.Origin.RemoteApply
			|| !_session.SessionActive
			|| HarmonyTraverse.IsGenerating())
		{
			return;
		}

		_world.SendDynamiteExplosion(itemId, new NetVector2(position.x, position.y));
		_log.LogInformation("[Dynamite] local explosion item {ItemId} at ({X:F1},{Y:F1}), origin={Origin}.",
			itemId, position.x, position.y, _session.Role == SessionRole.Host ? "HostBroadcast" : "Report");
	}

	private void OnRemote(ulong sender, ulong itemId, NetVector2 position)
	{
		if (itemId != 0 && !_seen.Add(itemId))
		{
			_log.LogWarning("[Dynamite] duplicate explosion item {ItemId} at ({X:F1},{Y:F1}) from {Sender} — dropped.", itemId, position.X, position.Y, sender);
			return;
		}

		var pos = new Vector2(position.X, position.Y);
		if (_session.Role == SessionRole.Host)
		{
			using (CallContext.Enter(CallContext.Origin.RemoteApply))
			{
				WorldGeneration.CreateExplosion(ExplosionParams(pos));
			}

			_world.BroadcastDynamiteExplosion(sender, itemId, position);
			_log.LogInformation("[Dynamite] host applied remote explosion item {ItemId} at ({X:F1},{Y:F1}) from {Sender}.",
				itemId, position.X, position.Y, sender);
		}
		else
		{
			using (CallContext.Enter(CallContext.Origin.RemoteApply))
			{
				_replay.ReplayExplosion(ExplosionParams(pos));
			}

			_log.LogInformation("[Dynamite] replayed explosion item {ItemId} at ({X:F1},{Y:F1}) from {Sender}.",
				itemId, position.X, position.Y, sender);
		}
	}
}
