using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Replays the gun muzzle-flash particle on a remote player's render clone.
/// The source side's <c>GunScript.Fire</c> plays its own
/// <c>muzzleParticle</c> (GunScript.cs:191); a render clone never runs that
/// native path, so the <see cref="CharacterSoundKind.GunFire"/> event receiver
/// locates the clone's gun (the one nearest to the reported fire position) and
/// calls <see cref="ParticleSystem.Play"/> directly. The clone is a display
/// proxy: this only fires the particle component, it never simulates the gun.
/// </summary>
internal static class MuzzleFlashReplay
{
	/// <summary>
	/// Plays the nearest clone gun's muzzle particle to <paramref name="firePosition"/>.
	/// Returns true when a particle was played; false when the clone has no usable
	/// gun particle yet (e.g. the 1 Hz inventory snapshot has not rendered the gun).
	/// </summary>
	internal static bool TryPlay(Body body, Vector2 firePosition)
	{
		if (body == null) // Unity object — == (is null misses destroyed clones)
		{
			return false;
		}

		GunScript? nearest = null;
		var nearestSqr = float.MaxValue;

		foreach (var gun in body.GetComponentsInChildren<GunScript>(true))
		{
			var particle = gun.muzzleParticle;
			if (particle == null) // Unity object — ==
			{
				continue;
			}

			var sqr = ((Vector2)gun.transform.position - firePosition).sqrMagnitude;
			if (sqr < nearestSqr)
			{
				nearestSqr = sqr;
				nearest = gun;
			}
		}

		if (nearest == null) // Unity object — ==
		{
			return false;
		}

		nearest.muzzleParticle.Play();
		return true;
	}
}
