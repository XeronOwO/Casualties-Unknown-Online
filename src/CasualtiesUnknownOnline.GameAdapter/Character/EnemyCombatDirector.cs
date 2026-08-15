using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Host-side enemy combat director. The enemy simulation is host-authoritative,
/// but the game's AI discovers players through PHYSICS queries that only see
/// colliders — and every remote render clone has its colliders disabled by
/// <see cref="RemoteBodyFactory"/> (they must never participate in physics).
/// The original single-player code therefore only ever targets the host body:
/// SpiderHandler.Update's OverlapCircle (SpiderHandler.cs:71) and
/// CrystalEnemy.body = PlayerCamera.main.body (CrystalEnemy.cs:15). This
/// director resolves the missing multiplayer targeting without re-enabling
/// clone colliders:
///  - when a spider recomputes its move target, the nearest in-world player
///    (host body + reported remote positions) wins;
///  - CrystalEnemy.body resolves to the nearest in-world player body within the
///    game's own 64-unit "close" radius (CrystalEnemy.cs:25);
///  - when the host's spider/crystal reaches a remote player, the host sends
///    the one-shot EnemyAttack command; the victim applies the game's own
///    damage method locally and reports the post-attack terminal state.
/// Local-host collisions stay on the game's native path (real colliders).
/// </summary>
internal sealed class EnemyCombatDirector(
	SessionService session,
	EntitySyncService entities,
	EnemySyncService enemies,
	EnemySyncCoordinator enemySync,
	RemotePlayerRenderer renderer,
	ILogger<EnemyCombatDirector> log)
{
	/// <summary>Spider bite range — FixedUpdate stops chasing at 1.5 units (SpiderHandler.cs:125), so contact happens inside that radius.</summary>
	private const float SpiderBiteRange = 1.5f;

	/// <summary>CrystalEnemy.close threshold (CrystalEnemy.cs:25) — the radius the game itself uses for player proximity.</summary>
	private const float CrystalCloseRange = 64f;

	/// <summary>Crystal Lunge raycasts 999 units (CrystalEnemy.cs:133) and ignores non-Body/non-Ground hits.</summary>
	private const float CrystalRayLength = 999f;

	/// <summary>Ray-vs-player tolerance (units) for the host's lunge arbitration — accept-first, not collision-box validation.</summary>
	private const float CrystalRayTolerance = 2f;

	private static readonly FieldInfo? BiteCooldownField =
		typeof(SpiderHandler).GetField("biteCooldown", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly SessionService _session = session;
	private readonly EntitySyncService _entities = entities;
	private readonly EnemySyncService _enemies = enemies;
	private readonly EnemySyncCoordinator _enemySync = enemySync;
	private readonly RemotePlayerRenderer _renderer = renderer;
	private readonly ILogger<EnemyCombatDirector> _log = log;

	private readonly List<EnemyTarget> _candidates = [];
	private int _candidateFrame = -1;
	private bool _biteFieldMissingLogged;

	/// <summary>Per-frame pump: host-ordered spider-bite arbitration (crystal lunge rides the Lunge patch callback).</summary>
	internal void Update()
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		foreach (var spider in UnityEngine.Object.FindObjectsOfType<SpiderHandler>())
		{
			TryOrderSpiderBite(spider);
		}
	}

	/// <summary>
	/// SpiderHandler.Update just recomputed its move target (the moveTime reset
	/// edge — moveTime is public, SpiderHandler.cs:95): replace the
	/// single-player OverlapCircle result with the nearest in-world player, but
	/// only inside the spider's own seeDistance. The game's retreat windows are
	/// preserved: after a bite moveTime is set to retreatMoveTime, so this edge
	/// does not fire until the retreat expires.
	/// </summary>
	internal void OnSpiderTargetDecided(SpiderHandler spider)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		var target = FindTarget(EnemyCombatArbitration.SelectNearest(
			Facts(), ToNetVector2(spider.transform.position), spider.seeDistance));
		if (target is null)
		{
			return;
		}

		spider.target = target.Position;
	}

	/// <summary>
	/// CrystalEnemy.body getter (the private property the whole AI reads) — return
	/// the nearest in-world player body inside the game's own 64-unit close radius.
	/// When no remote player is that close, the original PlayerCamera.main.body
	/// stays (the game's far behavior is unchanged).
	/// </summary>
	internal void ResolveCrystalTargetBody(CrystalEnemy crystal, ref Body body)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		var fact = EnemyCombatArbitration.SelectNearest(
			BuildCandidates().Where(c => c.Body != null).Select(c => c.ToFact()),
			ToNetVector2(crystal.transform.position),
			CrystalCloseRange);
		if (fact is { } selected && FindTarget(selected)?.Body is { } targetBody)
		{
			body = targetBody;
		}
	}

	/// <summary>
	/// CrystalEnemy.Lunge is starting on the host. The crystal is aimed at the
	/// nearest player (the property override above); if that player is a remote
	/// clone the game's RaycastAll cannot see it (no collider), so the host
	/// decides the hit here — nearest player along the lunge ray before the
	/// first ground hit — and orders the victim to apply the lunge locally.
	/// </summary>
	internal void OnCrystalLunge(CrystalEnemy crystal)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		var building = crystal.GetComponentInParent<BuildingEntity>();
		if (building == null || !_enemySync.TryGetHostEnemyId(building, out var enemyId)) // Unity object — ==
		{
			return;
		}

		var origin = new Vector2(crystal.transform.position.x, crystal.transform.position.y);
		var direction = new Vector2(crystal.transform.up.x, crystal.transform.up.y);
		var groundDistance = FirstGroundDistance(origin, direction, crystal.transform);
		var fact = EnemyCombatArbitration.SelectLungeVictim(
			Facts(), ToNetVector2(origin), ToNetVector2(direction), groundDistance, CrystalRayTolerance);
		var target = FindTarget(fact);
		if (target is null || target.SteamId == _session.LocalSteamId)
		{
			return; // no player in the ray, or the local body — the game's own raycast handles that natively
		}

		var limbIndex = SelectLimbIndex(target, origin);
		_enemies.SendEnemyAttack(new EnemyAttackMsg
		{
			EnemyId = enemyId.ToNetworkEntityIdMsg(),
			VictimSteamId = target.SteamId,
			Kind = EnemyAttackKind.CrystalLunge,
			LimbIndex = limbIndex,
		});
		_log.LogInformation("[Enemy] host crystal {Enemy} lunge ordered on {Victim} limb {Limb}.",
			enemyId, target.SteamId, limbIndex);
	}

	// ---- Spider bite (the host's collision callback can never touch a remote clone) ----

	private void TryOrderSpiderBite(SpiderHandler spider)
	{
		if (BiteCooldownField == null)
		{
			if (!_biteFieldMissingLogged)
			{
				_biteFieldMissingLogged = true;
				_log.LogError("[Enemy] SpiderHandler.biteCooldown field not found — host-ordered spider bites are disabled.");
			}

			return;
		}

		var cooldown = (float)BiteCooldownField.GetValue(spider);
		var fact = EnemyCombatArbitration.SelectBiteVictim(
			Facts(), ToNetVector2(spider.transform.position), SpiderBiteRange, cooldown, spider.stunTime, _session.LocalSteamId);
		var target = FindTarget(fact);
		if (target is null)
		{
			return; // cooldown/stun closed, nobody in bite range, or the local body rides the native collision path
		}

		var building = spider.GetComponentInParent<BuildingEntity>();
		if (building == null || !_enemySync.TryGetHostEnemyId(building, out var enemyId)) // Unity object — ==
		{
			return;
		}

		var limbIndex = SelectLimbIndex(target, spider.transform.position);
		_enemies.SendEnemyAttack(new EnemyAttackMsg
		{
			EnemyId = enemyId.ToNetworkEntityIdMsg(),
			VictimSteamId = target.SteamId,
			Kind = EnemyAttackKind.SpiderBite,
			LimbIndex = limbIndex,
		});

		// Mirror CheckForLimbDamage's post-bite retreat + cooldown write
		// (SpiderHandler.cs:146-151) so the host spider backs off exactly like
		// after a native bite and cannot double-order during the retreat.
		BiteCooldownField.SetValue(spider, spider.biteCoolToSet);
		var fromSpider = new Vector2(spider.transform.position.x - target.Position.x, spider.transform.position.y - target.Position.y);
		spider.target = fromSpider.normalized * 15f + new Vector2(spider.transform.position.x, spider.transform.position.y);
		spider.moveTime = spider.retreatMoveTime;
		_log.LogInformation("[Enemy] host spider {Enemy} bite ordered on {Victim} limb {Limb}.",
			enemyId, target.SteamId, limbIndex);
	}

	// ---- Target resolution ----

	private EnemyTarget? FindTarget(EnemyTargetFact? fact)
	{
		if (fact is not { } selected)
		{
			return null;
		}

		foreach (var candidate in BuildCandidates())
		{
			if (candidate.SteamId == selected.SteamId)
			{
				return candidate;
			}
		}

		return null;
	}

	private IEnumerable<EnemyTargetFact> Facts() => BuildCandidates().Select(c => c.ToFact());

	private static NetVector2 ToNetVector2(Vector2 value) => new(value.x, value.y);

	private float FirstGroundDistance(Vector2 origin, Vector2 direction, Transform self)
	{
		var hits = Physics2D.RaycastAll(origin, direction, CrystalRayLength, LayerMask.GetMask("Ground"));
		var best = CrystalRayLength;
		foreach (var hit in hits)
		{
			if (hit.collider == null || hit.transform == self || hit.distance < 0.01f) // Unity objects — ==
			{
				continue;
			}

			if (hit.distance < best)
			{
				best = hit.distance;
			}
		}

		return best;
	}

	private int SelectLimbIndex(EnemyTarget target, Vector2 from)
	{
		var body = target.SteamId == _session.LocalSteamId
			? LocalBody()
			: (_renderer.TryGetRemoteBody(target.SteamId, out var remoteBody) ? remoteBody : null);
		return body != null ? BodyLimbIndex(body, from) : -1; // Unity object — ==; -1 = the victim picks its closest limb
	}

	private static int BodyLimbIndex(Body body, Vector2 from)
	{
		var limb = body.GetClosestLimb(from);
		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity object — ==
			{
				return i;
			}
		}

		return -1;
	}

	private Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
	}

	private List<EnemyTarget> BuildCandidates()
	{
		if (_candidateFrame == Time.frameCount)
		{
			return _candidates;
		}

		_candidates.Clear();
		var localBody = LocalBody();
		if (localBody != null) // Unity object — ==
		{
			_candidates.Add(new EnemyTarget(
				_session.LocalSteamId,
				new Vector2(localBody.transform.position.x, localBody.transform.position.y),
				localBody));
		}

		foreach (var remote in _entities.RemotePlayers)
		{
			// StateReceivedMs < 0 = no report yet; the (0,0) buffer default would
			// drag enemies to the world origin.
			if (remote.StateReceivedMs < 0 || !_session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			_renderer.TryGetRemoteBody(remote.SteamId, out var remoteBody);
			_candidates.Add(new EnemyTarget(
				remote.SteamId,
				new Vector2(remote.Position.X, remote.Position.Y),
				remoteBody));
		}

		_candidateFrame = Time.frameCount;
		return _candidates;
	}

	/// <summary>One in-world player the host-side enemy AI may target (position from the authoritative entity stream; body for limb resolution when a clone exists).</summary>
	private sealed class EnemyTarget(ulong steamId, Vector2 position, Body? body)
	{
		internal ulong SteamId { get; } = steamId;

		internal Vector2 Position { get; } = position;

		internal Body? Body { get; } = body;

		internal EnemyTargetFact ToFact() => new(SteamId, new NetVector2(Position.x, Position.y));
	}
}
