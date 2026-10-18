using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The continuous bleeding values the HOST owns inside a character snapshot: the body's blood
/// volume and total bleeding speed and each limb's bleed amount (the 2026-10-18 ruling). ONE
/// definition is read by the two places that need it — <see cref="CharacterDataStore.SaveReportedCharacterData"/>
/// keeps a plain report from writing them, and the Game Adapter's host-clone simulation compares
/// the stored values with the ones it last wrote to recognize an event the host arbitrated.
/// </summary>
internal static class ContinuousVitals
{
	/// <summary>The disagreement a stored value must exceed before it counts as an authoritative write.</summary>
	internal const float AuthorityEpsilon = 0.0005f;

	/// <summary>
	/// Overwrite the owned values of <paramref name="next"/> with the ones
	/// <paramref name="previous"/> already carried, so a report cannot state them. A limb the
	/// previous snapshot did not carry has no authoritative value to preserve, and its report
	/// value is zeroed rather than kept: the report is never allowed to state an owned value,
	/// including for an index the host does not cover.
	/// </summary>
	internal static void Preserve(CharacterDataMsg previous, CharacterDataMsg next)
	{
		if (previous.Health is { } previousHealth && next.Health is { } nextHealth)
		{
			nextHealth.BloodVolume = previousHealth.BloodVolume;
			nextHealth.TotalBleedSpeed = previousHealth.TotalBleedSpeed;
		}

		foreach (var limb in next.Limbs)
		{
			var old = FindLimb(previous, limb.Index);
			limb.BleedAmount = old?.BleedAmount ?? 0f;
		}
	}

	/// <summary>
	/// True when the stored snapshot's owned values differ from the values the host clone last
	/// wrote — the recognition of an authoritative write. Comparing with the CLONE's current
	/// values would read the clone's own advance as a change; the last write is the stable
	/// baseline because a plain report is never allowed to move these values.
	/// </summary>
	internal static bool Differs(CharacterDataMsg data, float bloodVolume, float totalBleedSpeed, float[] limbBleeds)
	{
		if (data.Health is { } health
			&& (Math.Abs(health.BloodVolume - bloodVolume) > AuthorityEpsilon
				|| Math.Abs(health.TotalBleedSpeed - totalBleedSpeed) > AuthorityEpsilon))
		{
			return true;
		}

		foreach (var limb in data.Limbs)
		{
			if (limb.Index >= 0 && limb.Index < limbBleeds.Length
				&& Math.Abs(limb.BleedAmount - limbBleeds[limb.Index]) > AuthorityEpsilon)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Write the host clone's owned values into the stored snapshot; the caller remembers them as its last write.</summary>
	internal static void Write(CharacterDataMsg data, float bloodVolume, float totalBleedSpeed, float[] limbBleeds)
	{
		if (data.Health is { } health)
		{
			health.BloodVolume = bloodVolume;
			health.TotalBleedSpeed = totalBleedSpeed;
		}

		foreach (var limb in data.Limbs)
		{
			if (limb.Index >= 0 && limb.Index < limbBleeds.Length)
			{
				limb.BleedAmount = limbBleeds[limb.Index];
			}
		}
	}

	private static CharacterLimbMsg? FindLimb(CharacterDataMsg data, int index)
	{
		foreach (var limb in data.Limbs)
		{
			if (limb.Index == index)
			{
				return limb;
			}
		}

		return null;
	}
}
