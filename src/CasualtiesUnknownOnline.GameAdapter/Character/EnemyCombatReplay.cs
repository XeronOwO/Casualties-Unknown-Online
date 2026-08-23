using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The guest-side enemy combat replay: applies host-ordered attacks (spider
/// bite / crystal lunge) to the local body and reports the local victim's
/// post-bite / post-lunge terminal state as dedicated EnemyBite/EnemyLunge
/// events. It is deliberately separate from the enemy binding/stream side so
/// the enemy domain can keep its two responsibilities distinct.
/// </summary>
internal sealed class EnemyCombatReplay(
	ISessionControl session,
	EnemySyncService enemies,
	IMapper mapper,
	CharacterDataSync characterData,
	System.Func<NetworkEntityId, BuildingEntity?> findEntity,
	ILogger<EnemySyncCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly IMapper _mapper = mapper;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly System.Func<NetworkEntityId, BuildingEntity?> _findEntity = findEntity;
	private readonly ILogger<EnemySyncCoordinator> _log = log;

	// ---- Host-ordered enemy attacks (the dedicated command — never the snapshot) ----

	internal void OnEnemyAttackReceived(EnemyAttackMsg msg)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Guest)
		{
			return;
		}

		var entity = _findEntity(msg.EnemyId.ToNetworkEntityId());
		if (entity == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] attack {Kind} arrived for unknown enemy {Enemy} — the snapshot binding may not have arrived yet; command dropped.",
				msg.Kind, msg.EnemyId.ToNetworkEntityId());
			return;
		}

		switch (msg.Kind)
		{
			case EnemyAttackKind.SpiderBite:
				ApplyHostSpiderBite(entity, msg);
				break;
			case EnemyAttackKind.CrystalLunge:
				ApplyHostCrystalLunge(entity, msg);
				break;
			default:
				_log.LogWarning("[Enemy] unknown attack kind {Kind} for enemy {Enemy} — dropped.", msg.Kind, msg.EnemyId.ToNetworkEntityId());
				break;
		}
	}

	/// <summary>
	/// Apply the host-ordered spider bite to the LOCAL body using the frozen
	/// copy's own SpiderHandler (same prefab values, same DamageLimb virtual
	/// dispatch). Replicates CheckForLimbDamage's non-collision side effects
	/// (SpiderHandler.cs:148-160) around DamageLimb; the EnemyBitePatches
	/// postfix on DamageLimb reports the post-bite terminal state back to the
	/// host — the command and its report are the dedicated event chain.
	/// </summary>
	private void ApplyHostSpiderBite(BuildingEntity entity, EnemyAttackMsg msg)
	{
		var spider = entity.GetComponentInChildren<SpiderHandler>();
		var body = LocalBody();
		if (spider == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] spider bite {Enemy} could not be applied — attacker/victim body missing.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var limb = SelectLimb(body, msg.LimbIndex, entity.transform.position);
		if (limb == null)
		{
			_log.LogWarning("[Enemy] spider bite {Enemy} has no non-dismembered limb — dropped.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		Sound.Play(spider.biteSound, entity.transform.position, false, true, null, 1f, 1f, false, false);
		limb.body.eyeScareTime = 5f;
		limb.body.talker.Talk(Locale.GetCharacter("hitbycreature"), null, false, true);
		limb.body.happiness -= spider.happinessLoss;
		spider.PlayThreatMusic();
		spider.DamageLimb(limb); // the EnemyBite report fires from the DamageLimb postfix
		if (spider.hitConnected)
		{
			foreach (var connected in limb.connectedLimbs)
			{
				spider.DamageLimb(connected);
			}
		}

		_log.LogInformation("[Enemy] applied host spider bite {Enemy} to local limb {Limb}.", msg.EnemyId.ToNetworkEntityId(), limb);
	}

	/// <summary>
	/// Apply the host-ordered crystal lunge to the LOCAL body, reproducing
	/// CrystalEnemy.Lunge's player-damage branch exactly
	/// (CrystalEnemy.cs:143-156): closest non-dismembered limb, the same
	/// armor-reduced damage constants and body reactions. The post-lunge
	/// terminal state is reported as the dedicated EnemyLunge event.
	/// </summary>
	private void ApplyHostCrystalLunge(BuildingEntity entity, EnemyAttackMsg msg)
	{
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		var body = LocalBody();
		if (crystal == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} could not be applied — attacker/victim body missing.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var limb = SelectLimb(body, msg.LimbIndex, entity.transform.position);
		if (limb == null)
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} has no non-dismembered limb — dropped.", msg.EnemyId.ToNetworkEntityId());
			return;
		}

		var armorReduction = limb.GetArmorReduction();
		limb.DamageWearables(0.4f);
		limb.muscleHealth -= 35f / armorReduction;
		limb.skinHealth -= 50f / armorReduction;
		limb.pain += 60f / armorReduction;
		limb.bleedAmount += 15f / armorReduction;
		body.adrenaline += 70f;
		body.stamina = 100f;
		body.eyePanicTime = 0.5f;
		body.Scream();
		body.Ragdoll();
		body.DoGoreSound();
		Sound.Play("crystalenemylaugh", entity.transform.position, true, true, null, 1f, 1f, false, false);

		SendLocalCrystalLunge(body, limb, "applied host crystal lunge to local limb {Limb}");
		_log.LogInformation("[Enemy] applied host crystal lunge {Enemy} to local limb {Limb}.", msg.EnemyId.ToNetworkEntityId(), LimbIndexOf(body, limb));
	}

	internal void OnEnemyLungeReceived(ulong sender, EnemyLungeMsg msg) => _characterData.ApplyEnemyLunge(msg);

	/// <summary>
	/// The host's own crystal hit the host body natively (CrystalEnemy.Lunge ran
	/// on the real collider) — the verified post-lunge limb arrives from the
	/// EnemyCombatDirector's pre/post trace and leaves here as the dedicated
	/// EnemyLunge event, never the 1 Hz snapshot.
	/// </summary>
	internal void ReportLocalCrystalLunge(Limb limb)
	{
		if (!_session.SessionActive || limb.body == null) // Unity object — ==
		{
			return;
		}

		var body = limb.body;
		var limbIndex = LimbIndexOf(body, limb);
		if (limbIndex < 0)
		{
			return; // not a limb of the local body — nothing to report
		}

		SendLocalCrystalLunge(body, limb, "reported host-local crystal lunge on local limb {Limb}");
	}

	private void SendLocalCrystalLunge(Body body, Limb limb, string message)
	{
		var limbMsg = _mapper.Map<CharacterLimbMsg>(limb);
		limbMsg.Index = LimbIndexOf(body, limb);
		_enemies.SendEnemyLunge(new EnemyLungeMsg
		{
			VictimSteamId = _session.LocalSteamId,
			Limb = limbMsg,
			Adrenaline = body.adrenaline,
			Stamina = body.stamina,
		});
		_log.LogInformation("[Enemy] " + message + ".", limbMsg.Index);
	}

	private static Limb? SelectLimb(Body body, int limbIndex, Vector3 enemyPosition)
	{
		if (limbIndex >= 0 && limbIndex < body.limbs.Length)
		{
			var indexed = body.limbs[limbIndex];
			if (indexed != null && !indexed.dismembered) // Unity object — ==
			{
				return indexed;
			}
		}

		var closest = body.GetClosestLimb(enemyPosition);
		return closest != null && !closest.dismembered ? closest : null; // Unity object — ==
	}

	private static Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
	}

	// ---- Enemy bite (the dedicated trigger — never the 1 Hz snapshot) ----

	/// <summary>
	/// The local player was bitten (the game's DamageLimb already ran on the
	/// local body): capture the post-bite terminal state and send it as the
	/// dedicated EnemyBite event — guest → host report, host → guest broadcast
	/// (accept-first, no distance/legitimacy validation).
	/// </summary>
	internal void ReportEnemyBite(Limb limb)
	{
		if (!_session.SessionActive || limb.body == null) // Unity object — ==
		{
			return;
		}

		var body = limb.body;
		var limbIndex = LimbIndexOf(body, limb);
		if (limbIndex < 0)
		{
			return; // not a limb of the local body — nothing to report
		}

		var limbMsg = _mapper.Map<CharacterLimbMsg>(limb);
		limbMsg.Index = limbIndex;

		_enemies.SendEnemyBite(new EnemyBiteMsg
		{
			VictimSteamId = _session.LocalSteamId,
			Limb = limbMsg,
			VenomTotal = body.venomTotal,
			Adrenaline = body.adrenaline,
			Happiness = body.happiness,
		});
	}

	internal void OnEnemyBiteReceived(ulong sender, EnemyBiteMsg msg) => _characterData.ApplyEnemyBite(msg);

	private static int LimbIndexOf(Body body, Limb limb)
	{
		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity object — ==
			{
				return i;
			}
		}

		return -1;
	}
}
