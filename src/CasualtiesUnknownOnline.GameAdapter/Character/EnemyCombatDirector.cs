using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

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
///  - when the host's spider reaches a player or the crystal begins a lunge, the
///    host ANNOUNCES the attack to every in-world guest. The host owns the
///    enemy's action and its timing; whether the attack connected is judged by
///    each client against its own view (the 2026-09-18 ruling), so a spider
///    lunging at air is a legal outcome.
/// Local-host collisions stay on the game's native path (real colliders).
/// </summary>
internal sealed class EnemyCombatDirector(
	ISessionControl session,
	IEntitySyncControl entities,
	EnemySyncService enemies,
	EnemySyncCoordinator enemySync,
	RemotePlayerRenderer renderer,
	ILogger<EnemyCombatDirector> log)
{
	private static readonly FieldInfo? BiteCooldownField =
		typeof(SpiderHandler).GetField("biteCooldown", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? ThreatWorkaroundField =
		typeof(SpiderHandler).GetField("threatWorkaround", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly ISessionControl _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly EnemySyncCoordinator _enemySync = enemySync;
	private readonly EnemyTargetResolver _targets = new(session, entities, renderer);
	private readonly ILogger<EnemyCombatDirector> _log = log;

	private bool _biteFieldMissingLogged;

	/// <summary>Per-frame pump: host spider-bite announcements (the crystal lunge rides the Lunge patch callback).</summary>
	internal void Update()
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return;
		}

		foreach (var spider in Object.FindObjectsOfType<SpiderHandler>())
		{
			TryAnnounceSpiderBite(spider);
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

		var target = _targets.Find(EnemyCombatArbitration.SelectNearest(
			_targets.Facts(), ToNetVector2(spider.transform.position), spider.seeDistance));
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
			_targets.BuildCandidates().Where(c => c.Body != null).Select(c => c.ToFact()),
			ToNetVector2(crystal.transform.position),
			EnemyCombatPolicy.CrystalCloseRange);
		if (fact is { } selected && _targets.Find(selected)?.Body is { } targetBody)
		{
			body = targetBody;
		}
	}

	/// <summary>
	/// CrystalEnemy.Lunge is starting on the host. The crystal is aimed at the
	/// nearest player (the property override above) and the native Lunge applies
	/// whatever its own raycast finds on the host (real colliders only), so the
	/// pre-lunge trace still lets the postfix report the host's own terminal
	/// state. The attack itself is ANNOUNCED to every in-world guest, which judges
	/// on its own view whether the lunge ray reached its body.
	/// </summary>
	internal object? OnCrystalLungeBegin(CrystalEnemy crystal)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return null;
		}

		var building = crystal.GetComponentInParent<BuildingEntity>();
		if (building != null && _enemySync.TryGetHostEnemyId(building, out var enemyId)) // Unity object — ==
		{
			_enemies.SendEnemyAttack(enemyId, EnemyAttackKind.CrystalLunge);
			_log.LogInformation("[Enemy] host crystal {Enemy} lunge announced to the session.", enemyId);
		}

		var body = _targets.LocalBody();
		return body != null ? CrystalLungeTrace.Capture(body) : null; // Unity object — ==; the native raycast handles a local hit
	}

	/// <summary>
	/// CrystalEnemy.Lunge just finished on the host. The native method already
	/// applied the damage to the local body; the pre/post limb diff identifies
	/// the limb the game actually hit (it picks a random non-dismembered limb)
	/// and reports its post-lunge terminal state. No diff = no report.
	/// </summary>
	internal void OnCrystalLungeEnd(object? state)
	{
		if (state is not CrystalLungeTrace trace)
		{
			return;
		}

		var changed = trace.FindChangedLimb();
		if (changed == null) // Unity object — ==
		{
			_log.LogInformation("[Enemy] host-local crystal lunge produced no limb diff — no EnemyLunge report.");
			return;
		}

		_enemySync.ReportLocalCrystalLunge(changed);
	}

	// ---- Item hits (thrown/dropped items vs host-authoritative animals) ----

	/// <summary>
	/// A SpiderHandler.OnCollisionEnter2D completed on the host. The native item
	/// branch (SpiderHandler.cs:246-258) only runs inside 50 units of the LOCAL
	/// body — single-player scoping that breaks when a REMOTE guest throws an
	/// item far from the host. This entry generalizes the proximity guard to the
	/// in-world player set and returns the health damage for the dedicated
	/// BuildingEntityDamaged relay. When the native branch did not run it also
	/// applies the same local host-side effects (health, stun, sounds, item
	/// bounce) so the host authority is indistinguishable from a native hit.
	/// Returns null when there is no reportable item impact.
	/// </summary>
	internal float? OnEnemyItemCollision(SpiderHandler spider, Collision2D collision)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Host)
		{
			return null;
		}

		if (spider.GetComponentInParent<RemoteEnemyDriver>() != null) // Unity object — ==; a frozen render copy never reports
		{
			return null;
		}

		var item = collision.gameObject.GetComponent<Item>();
		if (item == null) // Unity object — ==
		{
			return null;
		}

		var magnitude = collision.relativeVelocity.magnitude;
		if (!EnemyItemHitArbitration.IsImpactEligible(magnitude))
		{
			return null;
		}

		var building = spider.GetComponentInParent<BuildingEntity>();
		if (building == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] item hit on {Spider} has no BuildingEntity — no host-side damage/report.",
				spider.transform.position);
			return null;
		}

		var localBody = _targets.LocalBody();
		var nativeHandled = localBody != null &&
			Vector2.Distance(spider.transform.position, localBody.transform.position) < EnemyItemHitArbitration.PlayerRadius;
		var anyInWorldPlayer = EnemyItemHitArbitration.AnyPlayerWithin(
			_targets.BuildCandidates().Select(c => c.ToFact().Position),
			ToNetVector2(spider.transform.position),
			EnemyItemHitArbitration.PlayerRadius);

		switch (EnemyCombatOrderPolicy.DecideItemHit(nativeHandled, anyInWorldPlayer))
		{
			case EnemyCombatOrderPolicy.ApplyPath.HostItemFallback:
				ApplyNativeItemBranch(spider, item, magnitude, building);
				break;
			case EnemyCombatOrderPolicy.ApplyPath.None:
				return null; // same as the single-player scoping: no player near, no item-vs-enemy effect
		}

		var damage = EnemyItemHitArbitration.ComputeHealthDamage(magnitude, item.rb.mass);
		_log.LogInformation("[Enemy] item hit on {Enemy} near host at ({X:F1},{Y:F1}) — damage {Damage:F2}, nativeHandled {Native}.",
			building.id, spider.transform.position.x, spider.transform.position.y, damage, nativeHandled);
		return damage;
	}

	/// <summary>
	/// Apply the native SpiderHandler item branch exactly (SpiderHandler.cs:
	/// 246-258) when the original skipped it because the local body was far
	/// away. The threat-workaround toggle is private and reflected; the field
	/// is locked by GameFieldContractTests.
	/// </summary>
	private void ApplyNativeItemBranch(SpiderHandler spider, Item item, float magnitude, BuildingEntity building)
	{
		var num = EnemyItemHitArbitration.ComputeImpactWeight(magnitude, item.rb.mass);
		Sound.Play("gore3", spider.transform.position, false, true, null, 1f, 1f, false, false);
		Sound.Play("boneHit", spider.transform.position, false, true, null, 1f, 1f, false, false);

		var spiderRb = spider.GetComponent<Rigidbody2D>();
		if (spiderRb != null && spiderRb.mass > 0f) // Unity object — ==
		{
			spiderRb.velocity = Vector2.Lerp(spiderRb.velocity, item.rb.velocity, 1f / spiderRb.mass * 10f);
		}

		item.rb.velocity *= -1f;
		building.health -= EnemyItemHitArbitration.ComputeHealthDamage(magnitude, item.rb.mass);

		ThreatWorkaroundField?.SetValue(spider, false);
		spider.AnimalHit(EnemyItemHitArbitration.ComputeStunDamage(magnitude, item.rb.mass));
		ThreatWorkaroundField?.SetValue(spider, true);
	}

	// ---- Spider bite (the host's collision callback can never touch a remote clone) ----

	/// <summary>
	/// The spider's bite ACTION fired on the HOST's view — announce it to the
	/// session. The host does not decide who was hit or which limb: every in-world
	/// guest judges the announcement against its own screen and its own body.
	/// <para>
	/// The action is edge-triggered per spider (<see cref="EnemyBiteAnnouncementState"/>):
	/// it announces once while the game's own gate holds (cooldown open, no stun, a
	/// player in range) and the latch clears when that gate closes, so the cadence
	/// follows the enemy's own cooldown instead of the frame rate. The local body
	/// keeps its native path — the game's own collision callback damages it and
	/// writes the cooldown, and this method must not touch either — while a remote
	/// nearest candidate makes the host mirror <c>CheckForLimbDamage</c>'s post-bite
	/// retreat + cooldown (SpiderHandler.cs:185-192) so its own spider backs off
	/// exactly like after a native bite.
	/// </para>
	/// <para>
	/// Every in-world guest is announced to, never only the nearest candidate: the
	/// nearest is the host's stale picture, and whether a screen shows the bite
	/// connecting is the judging client's question alone.
	/// </para>
	/// </summary>
	private void TryAnnounceSpiderBite(SpiderHandler spider)
	{
		if (BiteCooldownField == null)
		{
			if (!_biteFieldMissingLogged)
			{
				_biteFieldMissingLogged = true;
				_log.LogError("[Enemy] SpiderHandler.biteCooldown field not found — host spider bite announcements are disabled.");
			}

			return;
		}

		var cooldown = (float)BiteCooldownField.GetValue(spider);
		var fact = EnemyCombatArbitration.SelectBiteVictim(
			_targets.Facts(), ToNetVector2(spider.transform.position), EnemyCombatPolicy.SpiderBiteRange, cooldown, spider.stunTime);
		var state = AnnouncementState(spider);
		if (fact is not { } victim)
		{
			state.Announced = false; // the action ended — the next one announces again
			return; // cooldown/stun closed, or nobody in bite range
		}

		if (state.Announced)
		{
			return; // this bite action is already announced
		}

		var building = spider.GetComponentInParent<BuildingEntity>();
		if (building == null || !_enemySync.TryGetHostEnemyId(building, out var enemyId)) // Unity object — ==
		{
			return;
		}

		state.Announced = true;
		_enemies.SendEnemyAttack(enemyId, EnemyAttackKind.SpiderBite);

		if (victim.SteamId == _session.LocalSteamId)
		{
			// The native collision path owns the local body's bite — it damages the limb and
			// writes the cooldown itself — so nothing is mirrored here. The action is announced
			// anyway: the guests judge their OWN screens, and the host's own view (a stale
			// picture that merely has the host body nearest) must not decide that for them.
			_log.LogInformation("[Enemy] host spider {Enemy} bite action announced — the host's nearest bite candidate is its own body.",
				enemyId);
			return;
		}

		BiteCooldownField.SetValue(spider, spider.biteCoolToSet);
		var fromSpider = new Vector2(spider.transform.position.x - victim.Position.X, spider.transform.position.y - victim.Position.Y);
		spider.target = fromSpider.normalized * 15f + new Vector2(spider.transform.position.x, spider.transform.position.y);
		spider.moveTime = spider.retreatMoveTime;

		var biteDirection = new Vector2(
			victim.Position.X - spider.transform.position.x,
			victim.Position.Y - spider.transform.position.y);
		SpiderClawReplay.Play(spider, biteDirection);

		_log.LogInformation("[Enemy] host spider {Enemy} bite action announced — the host's nearest bite candidate is {Victim}.",
			enemyId, victim.SteamId);
	}

	/// <summary>The per-spider announcement latch — added on first use, since the spider prefab carries none.</summary>
	private static EnemyBiteAnnouncementState AnnouncementState(SpiderHandler spider)
	{
		var state = spider.GetComponent<EnemyBiteAnnouncementState>();
		return state != null ? state : spider.gameObject.AddComponent<EnemyBiteAnnouncementState>(); // Unity object — ==
	}

	// ---- Target resolution helpers ----

	private static NetVector2 ToNetVector2(Vector2 value) => new(value.x, value.y);
}
