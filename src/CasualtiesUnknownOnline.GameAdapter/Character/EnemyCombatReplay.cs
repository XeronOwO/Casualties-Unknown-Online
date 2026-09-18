using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityRandom = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The guest-side enemy combat replay: it JUDGES an announced enemy attack
/// against this client's own view — the frozen enemy copy it renders and its own
/// body — and applies the game's own damage locally only when that view shows the
/// attack connecting (the 2026-09-18 ruling: the host announces the enemy's
/// action, the client the effect lands on decides it). The local victim's
/// post-bite / post-lunge terminal state leaves as the dedicated EnemyBite /
/// EnemyLunge events. It is deliberately separate from the enemy binding/stream
/// side so the enemy domain can keep its two responsibilities distinct.
/// </summary>
internal sealed class EnemyCombatReplay(
	ISessionControl session,
	EnemySyncService enemies,
	IMapper mapper,
	CharacterDataSync characterData,
	Func<NetworkEntityId, BuildingEntity?> findEntity,
	ILogger<EnemySyncCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly EnemySyncService _enemies = enemies;
	private readonly IMapper _mapper = mapper;
	private readonly CharacterDataSync _characterData = characterData;
	private readonly Func<NetworkEntityId, BuildingEntity?> _findEntity = findEntity;
	private readonly ILogger<EnemySyncCoordinator> _log = log;
	private readonly EnemyAttackLedger _ledger = new();

	// ---- Announced enemy attacks (the dedicated broadcast — never the snapshot) ----

	internal void OnEnemyAttackReceived(EnemyAttackMsg msg)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Guest)
		{
			return;
		}

		var enemyId = msg.EnemyId.ToNetworkEntityId();
		if (!_ledger.ShouldJudge(enemyId, msg.AttackSeq))
		{
			_log.LogInformation("[Enemy] attack {Kind} #{Seq} of enemy {Enemy} was already judged — ignored.",
				msg.Kind, msg.AttackSeq, enemyId);
			return;
		}

		var entity = _findEntity(enemyId);
		if (entity == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] attack {Kind} #{Seq} arrived for unknown enemy {Enemy} — the snapshot binding may not have arrived yet; the attack is dropped.",
				msg.Kind, msg.AttackSeq, enemyId);
			return;
		}

		switch (msg.Kind)
		{
			case EnemyAttackKind.SpiderBite:
				JudgeSpiderBite(entity, enemyId, msg.AttackSeq);
				break;
			case EnemyAttackKind.CrystalLunge:
				JudgeCrystalLunge(entity, enemyId, msg.AttackSeq);
				break;
			default:
				_log.LogWarning("[Enemy] unknown attack kind {Kind} for enemy {Enemy} — dropped.", msg.Kind, enemyId);
				break;
		}
	}

	/// <summary>
	/// Judge the announced spider bite on this client's own view: the bite lands
	/// only when the frozen spider's collider touches one of this body's limbs
	/// here, with the game's own facing gate. A bite this screen never shows
	/// connecting does NOT land — that is the intended semantics (the host's
	/// spider biting air), not a lost command.
	/// </summary>
	private void JudgeSpiderBite(BuildingEntity entity, NetworkEntityId enemyId, uint attackSeq)
	{
		var spider = entity.GetComponentInChildren<SpiderHandler>();
		var body = LocalBody();
		if (spider == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] spider bite {Enemy} #{Seq} could not be judged — attacker/victim body missing.", enemyId, attackSeq);
			return;
		}

		var limbIndex = EnemyAttackLocalProbe.ProbeBittenLimb(spider, body);
		if (limbIndex < 0)
		{
			_log.LogInformation("[Enemy] spider bite {Enemy} #{Seq} judged a miss — the local view shows no contact with this body.",
				enemyId, attackSeq);
			return;
		}

		ApplySpiderBite(spider, body, limbIndex);
		_log.LogInformation("[Enemy] spider bite {Enemy} #{Seq} judged a hit on local limb {Limb}.", enemyId, attackSeq, limbIndex);
	}

	/// <summary>
	/// Apply the judged bite to the LOCAL body using the frozen copy's own
	/// SpiderHandler (same prefab values, same DamageLimb virtual dispatch),
	/// replicating CheckForLimbDamage's non-collision side effects
	/// (SpiderHandler.cs:185-208) around DamageLimb; the EnemyBitePatches postfix
	/// on DamageLimb reports the post-bite terminal state back to the session.
	/// </summary>
	private void ApplySpiderBite(SpiderHandler spider, Body body, int limbIndex)
	{
		var limb = limbIndex >= 0 && limbIndex < body.limbs.Length ? body.limbs[limbIndex] : null;
		if (limb == null || limb.dismembered) // Unity object — ==
		{
			_log.LogWarning("[Enemy] judged spider bite has no usable limb {Limb} — dropped.", limbIndex);
			return;
		}

		Sound.Play(spider.biteSound, spider.transform.position, false, true, null, 1f, 1f, false, false);
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

		var biteDirection = new Vector2(
			body.transform.position.x - spider.transform.position.x,
			body.transform.position.y - spider.transform.position.y);
		SpiderClawReplay.Play(spider, biteDirection);
	}

	/// <summary>
	/// Judge the announced crystal lunge on this client's own view: the lunge
	/// lands only when the game's own ray (from the frozen crystal, along its
	/// displayed facing) reaches this client's body before the ground.
	/// </summary>
	private void JudgeCrystalLunge(BuildingEntity entity, NetworkEntityId enemyId, uint attackSeq)
	{
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		var body = LocalBody();
		if (crystal == null || body == null) // Unity objects — ==
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} #{Seq} could not be judged — attacker/victim body missing.", enemyId, attackSeq);
			return;
		}

		if (!EnemyAttackLocalProbe.CrystalLungeHitsLocalBody(crystal, body))
		{
			_log.LogInformation("[Enemy] crystal lunge {Enemy} #{Seq} judged a miss — the local ray reaches no body of this client.",
				enemyId, attackSeq);
			return;
		}

		var limb = SelectRandomLimb(body);
		if (limb == null) // Unity object — ==
		{
			_log.LogWarning("[Enemy] crystal lunge {Enemy} #{Seq} has no non-dismembered limb — dropped.", enemyId, attackSeq);
			return;
		}

		ApplyCrystalLunge(crystal, body, limb);
		_log.LogInformation("[Enemy] crystal lunge {Enemy} #{Seq} judged a hit on local limb {Limb}.",
			enemyId, attackSeq, LimbIndexOf(body, limb));
	}

	/// <summary>
	/// Apply the judged lunge to the LOCAL body, reproducing CrystalEnemy.Lunge's
	/// player-damage branch exactly (CrystalEnemy.cs:143-156): the same
	/// armor-reduced damage constants and body reactions. The post-lunge terminal
	/// state is reported as the dedicated EnemyLunge event.
	/// </summary>
	private void ApplyCrystalLunge(CrystalEnemy crystal, Body body, Limb limb)
	{
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
		Sound.Play("crystalenemylaugh", crystal.transform.position, true, true, null, 1f, 1f, false, false);

		SendLocalCrystalLunge(body, limb, "judged and applied an announced crystal lunge to local limb {Limb}");
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

	/// <summary>
	/// The game's own lunge limb choice: a random non-dismembered limb of the body
	/// the ray reached (CrystalEnemy.cs:141 — PickRandom over the filtered list).
	/// The victim's own body decides it; the attacker no longer names a limb.
	/// </summary>
	private static Limb? SelectRandomLimb(Body body)
	{
		var candidates = new List<Limb>();
		foreach (var limb in body.limbs)
		{
			if (limb != null && !limb.dismembered) // Unity object — ==
			{
				candidates.Add(limb);
			}
		}

		return candidates.Count == 0 ? null : candidates[UnityRandom.Range(0, candidates.Count)];
	}

	private static Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
	}

	// ---- Enemy bite (the dedicated trigger — never the 1 Hz snapshot) ----

	/// <summary>
	/// The local player was bitten (the game's DamageLimb already ran on the
	/// local body, whether from the native path or from a judged announcement):
	/// capture the post-bite terminal state and send it as the dedicated
	/// EnemyBite event — guest → host report, host → guest broadcast
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
