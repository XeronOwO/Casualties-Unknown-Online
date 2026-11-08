using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The host's plausibility bound for a member's full limb report. The affected side is still the judge of its own
/// body (the report is how a wound reaches the host), but the receiver holds the member's previous record, so a
/// jump the game's own rates cannot produce in one step is refused whole: a severed limb cannot come back, skin and
/// muscle heal slowly, infection clears slowly. The console's whole-body <c>heal</c> is exactly such a jump.
/// </summary>
internal static class LimbStatePlausibility
{
	/// <summary>The most skin or muscle health one event may add (the game's own mend heals far slower).</summary>
	internal const float MaxHealStep = 40f;

	/// <summary>The most infection one event may clear (the game's own cure is gradual).</summary>
	internal const float MaxCureStep = 40f;

	internal static LimbStateRefusalReason Check(IReadOnlyList<CharacterLimbMsg> previous, IReadOnlyList<CharacterLimbMsg> reported)
	{
		if (previous.Count == 0 || reported.Count == 0)
		{
			return LimbStateRefusalReason.None;
		}

		foreach (var now in reported)
		{
			var before = Find(previous, now.Index);
			if (before is null)
			{
				continue;
			}

			if (before.Dismembered && !now.Dismembered)
			{
				return LimbStateRefusalReason.DismemberedLimbReturned;
			}

			if (now.SkinHealth - before.SkinHealth > MaxHealStep)
			{
				return LimbStateRefusalReason.SkinJumped;
			}

			if (now.MuscleHealth - before.MuscleHealth > MaxHealStep)
			{
				return LimbStateRefusalReason.MuscleJumped;
			}

			if (before.InfectionAmount - now.InfectionAmount > MaxCureStep)
			{
				return LimbStateRefusalReason.InfectionCleared;
			}
		}

		return LimbStateRefusalReason.None;
	}

	private static CharacterLimbMsg? Find(IReadOnlyList<CharacterLimbMsg> limbs, int index)
	{
		foreach (var limb in limbs)
		{
			if (limb.Index == index)
			{
				return limb;
			}
		}

		return null;
	}
}
