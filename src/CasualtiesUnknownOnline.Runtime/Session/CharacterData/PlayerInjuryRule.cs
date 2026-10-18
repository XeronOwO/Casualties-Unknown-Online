using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The trigger rule for the owner's injury event: a limb's bleed rose since the last check. The
/// host's own continuous model can only DECAY a limb's bleed (native <c>Limb.Update</c> clamps and
/// decays, it never adds), and the 10 Hz row can only lower it — so a rise is a client-judged
/// injury (a trap, an explosion, a fall, a native wound) that the plain report is not allowed to
/// carry. The event then sends the owner's full post-injury limb/body state so the host's model
/// starts from the wound instead of erasing it.
///
/// <para>
/// A tiny epsilon keeps float noise and the decay/rounding of the host row out of the trigger; the
/// smallest real wound is far above it.
/// </para>
/// </summary>
internal static class PlayerInjuryRule
{
	/// <summary>The rise a limb must show before it counts as an injury rather than numeric noise.</summary>
	internal const float BleedIncreaseEpsilon = 0.001f;

	/// <summary>True when any limb's bleed rose above the baseline by more than the epsilon.</summary>
	internal static bool AnyBleedRose(IReadOnlyList<float> baseline, IReadOnlyList<float> current)
	{
		for (var i = 0; i < baseline.Count && i < current.Count; i++)
		{
			if (current[i] > baseline[i] + BleedIncreaseEpsilon)
			{
				return true;
			}
		}

		return false;
	}
}
