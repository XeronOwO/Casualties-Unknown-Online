using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote clones' limb-presentation rendering: each limb of a clone shows
/// its owner's synced wound state (broken bone sprite, dismembered = gone,
/// blood overlay + drip particles, skin/muscle wound shading, infection tint).
/// The clone's own Limb.Update is SKIPPED (BodyPatches.LimbUpdatePatch — its
/// vitals are not simulated), so every visual is applied directly here from
/// the owner's character snapshot (the 1 Hz <see cref="CharacterDataMsg"/>
/// carries the full per-limb set; the dedicated limb-state event updates it
/// between snapshots). The pure formulas live in <see cref="LimbPresentation"/>
/// and mirror the game's own visual code:
/// - broken: the brokenBone sprite child (Limb.MakeBoneSprite, Limb.cs:250-259
///   — private, replicated with public surface)
/// - dismembered: the limb GameObject deactivates (Limb.cs:115-116/139/186-188)
/// - bleed: _BloodOverlay = furBloodAmount + particle emission above the
///   game's 0.95 threshold (Limb.FurBloodUpdate, Limb.cs:445-476)
/// - wounds + shader params: the full _SkinDamage/_MuscleDamage/
///   _InfectionPercent/_SnowAmount/_Dirtyness/_Pain/_BloodOverlay/_Wetness
///   set (Limb.Update, Limb.cs:501-506; Limb.FurBloodUpdate, Limb.cs:487-488)
/// The remaining public limb fields are mirrored for fidelity (dislocated
/// changes the head's mouth expression on the clone's live FacialExpression,
/// FacialExpression.cs:94; splinted/pain/timers are read by the visuals that
/// still run). Every apply is idempotent (diff against the snapshot + the
/// RemoteCloneLimbRender marker on our own bone sprites).
/// </summary>
internal sealed class CloneLimbRenderer(ILogger<CloneLimbRenderer> log)
{
	private readonly ILogger<CloneLimbRenderer> _log = log;

	internal void ApplyCloneLimbs(Body clone, CharacterDataMsg data)
	{
		foreach (var limbData in data.Limbs)
		{
			if (limbData.Index < 0 || limbData.Index >= clone.limbs.Length)
			{
				_log.LogWarning("[CloneLimb] limb index {Index} out of range (0..{Max}) — skipped.", limbData.Index, clone.limbs.Length - 1);
				continue;
			}

			var limb = clone.limbs[limbData.Index];
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			ApplyLimb(limb, limbData, data.Health);
		}
	}

	private static void ApplyLimb(Limb limb, CharacterLimbMsg limbData, CharacterHealthMsg? body)
	{
		// Dismembered: the limb is gone (game semantics — SetActive(false)).
		// Applied in BOTH directions: a clone Instantiate copies the template's
		// active state, so a limb that must exist re-arms, and a snapshot limb
		// that must be gone deactivates.
		limb.dismembered = limbData.Dismembered;
		if (LimbPresentation.MustSetActive(limbData.Dismembered, limb.gameObject.activeSelf))
		{
			limb.gameObject.SetActive(!limbData.Dismembered);
		}

		// Broken: the brokenBone sprite child (Limb.MakeBoneSprite replica —
		// private in the game, so the renderer owns its own copy, marked so a
		// later apply destroys exactly ours, never the game's children).
		limb.broken = limbData.Broken;
		var bone = limb.GetComponentsInChildren<Transform>(true)
			.Select(t => t.gameObject)
			.FirstOrDefault(go => go.GetComponent<RemoteCloneLimbRender>() != null);
		if (limbData.Broken && bone == null)
		{
			CreateBoneSprite(limb);
		}
		else if (!limbData.Broken && bone != null)
		{
			Object.Destroy(bone);
		}

		// State mirrors (inert on the frozen clone — fidelity for the visuals
		// and readers that still run; dislocated is the visible one through
		// FacialExpression).
		limb.splinted = limbData.Splinted;
		limb.dislocated = limbData.Dislocated;
		limb.infected = limbData.Infected;
		limb.blockedBleeding = limbData.BlockedBleeding;
		limb.skinHealth = limbData.SkinHealth;
		limb.muscleHealth = limbData.MuscleHealth;
		limb.infectionAmount = limbData.InfectionAmount;
		limb.bleedAmount = limbData.BleedAmount;
		limb.furBloodAmount = limbData.FurBloodAmount;
		limb.pain = limbData.Pain;
		limb.dislocationTimer = limbData.DislocationTimer;
		limb.boneHealTimer = limbData.BoneHealTimer;
		limb.disinfectionTime = limbData.DisinfectionTime;
		limb.shrapnel = limbData.Shrapnel;
		limb.bandageSlowAmount = limbData.BandageSlowAmount;
		limb.skinHealAmount = limbData.SkinHealAmount;

		// The limb's per-clone material instance (Limb.Awake, Limb.cs:407-408)
		// — the game's own visual formulas, fed the synced values.
		var renderer = limb.GetComponent<SpriteRenderer>();
		if (renderer != null) // Unity object — ==
		{
			var mat = renderer.sharedMaterial;
			if (mat != null) // Unity object — ==
			{
				mat.SetFloat("_SkinDamage", LimbPresentation.SkinDamage(limbData.SkinHealth));
				mat.SetFloat("_MuscleDamage", LimbPresentation.MuscleDamage(limbData.MuscleHealth));
				mat.SetFloat("_InfectionPercent", LimbPresentation.InfectionPercent(limbData.InfectionAmount));
				mat.SetFloat("_SnowAmount", LimbPresentation.SnowAmount(body?.SnowAmount ?? 0f, body?.Dirtyness ?? 0f));
				mat.SetFloat("_Dirtyness", LimbPresentation.DirtynessAmount(body?.Dirtyness ?? 0f));
				mat.SetFloat("_Pain", LimbPresentation.PainAmount(limbData.Pain, body?.Adrenaline ?? 0f));
				mat.SetFloat("_BloodOverlay", limbData.FurBloodAmount);
				mat.SetFloat("_Wetness", LimbPresentation.WetnessAmount(body?.Wetness ?? 0f));
			}
		}

		// Drip particles above the game's own emission threshold
		// (Limb.FurBloodUpdate, Limb.cs:463-471: rate 5 while furBlood > 0.95).
		var bleed = limb.GetComponentInChildren<BleedParticle>(true);
		if (bleed != null) // Unity object — ==
		{
			var emission = bleed.GetComponent<ParticleSystem>().emission;
			emission.rateOverTime = LimbPresentation.BloodEmissionRate(limbData.FurBloodAmount);
		}
	}

	/// <summary>The game's MakeBoneSprite (Limb.cs:250-259), replicated with the public surface: a "brokenBone" sprite child above the limb, default material, RemoteCloneLimbRender-marked.</summary>
	private static void CreateBoneSprite(Limb limb)
	{
		var sprite = Resources.Load<Sprite>("brokenBone");
		if (sprite == null) // Unity object — ==
		{
			return;
		}

		var bone = new GameObject("RemoteBrokenBone", typeof(SpriteRenderer));
		bone.transform.SetParent(limb.transform);
		bone.transform.localPosition = Vector3.zero;
		bone.transform.eulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
		var boneRenderer = bone.GetComponent<SpriteRenderer>();
		boneRenderer.sprite = sprite;
		boneRenderer.sortingOrder = limb.GetComponent<SpriteRenderer>().sortingOrder + 1;
		boneRenderer.material = WorldGeneration.world.defaultMat;
		bone.AddComponent<RemoteCloneLimbRender>();
	}
}
