using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The continuous bleeding values the HOST owns inside a character snapshot: the body's
/// blood volume and each limb's bleed amount (the 2026-10-18 ruling). Two places read this
/// one definition — <see cref="CharacterDataStore.SaveReportedCharacterData"/> keeps a plain
/// report from writing them, and <see cref="PlayerVitalsService"/> compares them to decide
/// whether an event (the only writer the host allows) has landed.
/// </summary>
internal static class ContinuousVitals
{
	/// <summary>
	/// Overwrite the owned values of <paramref name="next"/> with the ones
	/// <paramref name="previous"/> already carried, so a report cannot state them. A limb the
	/// previous snapshot did not carry has no authoritative value to preserve, and its report
	/// value is zeroed rather than kept: the report is never allowed to state an owned value,
	/// including for an index the model does not cover.
	/// </summary>
	internal static void Preserve(CharacterDataMsg previous, CharacterDataMsg next)
	{
		if (previous.Health is { } previousHealth && next.Health is { } nextHealth)
		{
			nextHealth.BloodVolume = previousHealth.BloodVolume;
		}

		foreach (var limb in next.Limbs)
		{
			var old = FindLimb(previous, limb.Index);
			limb.BleedAmount = old?.BleedAmount ?? 0f;
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
