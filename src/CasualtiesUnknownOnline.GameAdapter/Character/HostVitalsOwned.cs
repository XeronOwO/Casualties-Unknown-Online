using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The body half of the host-owned vitals round trip: read the three values off the simulated
/// clone, put an authoritative snapshot's values ON the clone, and put previously captured values
/// back after an input map overwrote them. The snapshot half — the write, the comparison against
/// the last write and the report filter — is <c>ContinuousVitals</c> in the Runtime, one
/// definition for the store and the adapter.
/// </summary>
internal static class HostVitalsOwned
{
	/// <summary>Read the clone's owned values; <paramref name="bleeds"/> is indexed by limb index.</summary>
	internal static void Capture(Body body, float[] bleeds, out float bloodVolume, out float totalBleedSpeed)
	{
		bloodVolume = body.bloodVolume;
		totalBleedSpeed = body.totalBleedSpeed;
		for (var i = 0; i < body.limbs.Length && i < bleeds.Length; i++)
		{
			bleeds[i] = body.limbs[i].bleedAmount;
		}
	}

	/// <summary>Copy the snapshot's owned values onto the clone — the one authority direction.</summary>
	internal static void Apply(CharacterDataMsg data, Body body)
	{
		if (data.Health is { } health)
		{
			body.bloodVolume = health.BloodVolume;
			body.totalBleedSpeed = health.TotalBleedSpeed;
		}

		foreach (var limb in data.Limbs)
		{
			if (limb.Index >= 0 && limb.Index < body.limbs.Length)
			{
				body.limbs[limb.Index].bleedAmount = limb.BleedAmount;
			}
		}
	}

	/// <summary>Put previously captured owned values back on the clone after an input map overwrote them.</summary>
	internal static void Restore(Body body, float bloodVolume, float totalBleedSpeed, float[] bleeds)
	{
		body.bloodVolume = bloodVolume;
		body.totalBleedSpeed = totalBleedSpeed;
		for (var i = 0; i < body.limbs.Length && i < bleeds.Length; i++)
		{
			body.limbs[i].bleedAmount = bleeds[i];
		}
	}
}
