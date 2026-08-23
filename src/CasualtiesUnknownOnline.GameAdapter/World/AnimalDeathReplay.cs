using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The remote-side presentation-only half of an animal entity's death.
/// BuildingEntity.Update sends <c>AnimalDeath</c> to the local creature when an
/// attacker destroys it (BuildingEntity.cs:69-72). A remote death has its
/// Update suppressed by <see cref="RemoteEntityDeath"/> so the remote side
/// never rolls drops or awards the local experience; this class replays only
/// the observable creature-specific death effects (sound + gore/crystal
/// visual), deliberately excluding the attacker-side experience reward.
/// </summary>
internal static class AnimalDeathReplay
{
	/// <summary>
	/// Replays the creature-specific death presentation for a remotely-destroyed
	/// animal entity. The known creature families:
	/// <list type="bullet">
	/// <item><see cref="SpiderHandler"/> (including <c>SpiderHandlerTBE</c>): <c>gore</c> sound and, when
	/// <c>doDeathExplode</c> is set, the <c>BloodExplosion</c> effect.</item>
	/// <item><see cref="CrystalEnemy"/>: <c>crystalenemydeath</c> sound + the
	/// <c>Special/CrystalDistort</c> death animation.</item>
	/// <item><see cref="TraderScript"/>: <c>gore</c> sound + <c>BloodExplosion</c>
	/// at the trader's torso.</item>
	/// </list>
	/// If the entity carries none of these scripts, this is a no-op — the generic
	/// destruction replay still covers non-animal building entities.
	/// </summary>
	internal static void Replay(BuildingEntity entity)
	{
		var spider = entity.GetComponent<SpiderHandler>();
		if (spider != null) // Unity object — ==
		{
			Sound.Play("gore", entity.transform.position, false, true, null, 1f, 1f, false, false);
			if (spider.doDeathExplode)
			{
				Object.Instantiate(Resources.Load("BloodExplosion"), entity.transform.position, Quaternion.identity);
			}

			return;
		}

		var crystal = entity.GetComponent<CrystalEnemy>();
		if (crystal != null) // Unity object — ==
		{
			Sound.Play("crystalenemydeath", entity.transform.position, false, false, null, 1f, 1f, false, false);
			Utils.Create("Special/CrystalDistort", entity.transform.position, 0f).AddComponent<CrystalDeathAnimation>();
			return;
		}

		var trader = entity.GetComponent<TraderScript>();
		if (trader != null) // Unity object — ==
		{
			var position = trader.torso != null ? trader.torso.transform.position : entity.transform.position; // Unity object — ==
			Sound.Play("gore", position, false, true, null, 1f, 1f, false, false);
			Object.Instantiate(Resources.Load("BloodExplosion"), position, Quaternion.identity);
		}
	}
}
