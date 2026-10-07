namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The limb a cross-player effect lands on, resolved on the AFFECTED side's own
/// body: the operator's own pick when it is a real, attached limb of this body,
/// otherwise the most injured one — the same automatic rule the host applies for
/// the heal slice (<c>RemoteHealApplication.PickMostInjuredLimb</c>, pinned by
/// <c>InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured</c>).
/// <para>
/// Shared by the two migrated limb-use chains (injection from Part A, topical
/// from Part B of <c>mod-cross-player-native-semantics</c>) so the rule exists
/// once: the limb is a fact about the patient's body, and only that body's own
/// client can answer it.
/// </para>
/// </summary>
internal static class NativeLimbTarget
{
	internal static Limb? Resolve(Body body, int requestedLimbIndex)
	{
		if (requestedLimbIndex >= 0
			&& requestedLimbIndex < body.limbs.Length
			&& body.limbs[requestedLimbIndex] != null // Unity object — ==
			&& !body.limbs[requestedLimbIndex].dismembered)
		{
			return body.limbs[requestedLimbIndex];
		}

		Limb? best = null;
		var bestScore = float.MaxValue;
		foreach (var candidate in body.limbs)
		{
			if (candidate == null || candidate.dismembered) // Unity object — ==
			{
				continue;
			}

			var score = candidate.skinHealth + candidate.muscleHealth;
			if (best is null || score < bestScore)
			{
				best = candidate;
				bestScore = score;
			}
		}

		return best;
	}
}
