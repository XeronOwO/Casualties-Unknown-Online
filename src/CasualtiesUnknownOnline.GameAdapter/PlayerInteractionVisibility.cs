using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Game-backed direct player-interaction visibility oracle. It resolves the two
/// players' world positions from the authoritative entity stream (the local
/// body when available) and runs the same Ground-only linecast the vanilla
/// pickup check uses. Missing evidence is deliberately NOT treated as a block:
/// the gate only stops an action when a wall is confirmed between the pair,
/// keeping the accept-first/no-missing-sync-blocking model intact.
/// </summary>
public sealed class PlayerInteractionVisibility(
	ISessionControl session,
	IEntitySyncControl entities,
	ILogger<PlayerInteractionVisibility> log) : IPlayerInteractionVisibility
{
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ILogger<PlayerInteractionVisibility> _log = log;

	public bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId)
	{
		if (!TryGetPosition(observerSteamId, out var observer)
			|| !TryGetPosition(targetSteamId, out var target))
		{
			_log.LogDebug(
				"[Visibility] no world position for {Observer}/{Target} — interaction not blocked by visibility.",
				observerSteamId, targetSteamId);
			return true;
		}

		var hit = Physics2D.Linecast(observer, target, LayerMask.GetMask("Ground"));
		if (hit)
		{
			_log.LogInformation(
				"[Visibility] blocked {Observer} → {Target} by ground at ({HitX:F1},{HitY:F1}).",
				observerSteamId, targetSteamId, hit.point.x, hit.point.y);
			return false;
		}

		_log.LogDebug("[Visibility] clear {Observer} → {Target}.", observerSteamId, targetSteamId);
		return true;
	}

	private bool TryGetPosition(ulong steamId, out Vector2 position)
	{
		if (steamId == _session.LocalSteamId)
		{
			var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
			if (body != null) // Unity object — ==
			{
				position = body.transform.position;
				return true;
			}
		}

		var entity = steamId == _session.LocalSteamId
			? _entities.LocalPlayer
			: _entities.GetRemotePlayer(steamId);
		if (entity is null)
		{
			position = default;
			return false;
		}

		position = new Vector2(entity.Position.X, entity.Position.Y);
		return true;
	}
}
