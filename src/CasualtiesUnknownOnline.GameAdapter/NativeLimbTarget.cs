namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The limb a cross-player effect lands on, resolved on the AFFECTED side's own
/// body. TWO verdicts live here and the difference is the point:
/// <see cref="ResolveNamed"/> answers with the limb the request NAMED and with
/// nothing when this body cannot serve it, while <see cref="Resolve"/> keeps the
/// automatic rule the INJECTION chain uses — the operator's pick
/// when it is a real, attached limb of this body, otherwise the most injured one
/// (the same rule the host applies for the heal slice,
/// <c>RemoteHealApplication.PickMostInjuredLimb</c>, pinned by
/// <c>InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured</c>),
/// where a -1 limb is a legal auto-select.
/// <para>
/// The limb-tool AND topical families ask the NAMED question and nothing else
/// (decision 246): for both, the limb IS that action, the host refuses a request that
/// names none, and substituting another limb is the very defect the rule names. Shared
/// by the chains that landed from <c>mod-cross-player-native-semantics</c> so each
/// verdict exists once: the limb is a fact about the patient's body, and only that
/// body's own client can answer it.
/// </para>
/// </summary>
internal static class NativeLimbTarget
{
	/// <summary>
	/// The limb <paramref name="requestedLimbIndex"/> names on this body, or
	/// <see langword="null"/> when this body cannot serve it: no index at all, an
	/// index past its layout, a limb that is gone, or a dismembered one. A caller
	/// that asks this question REFUSES on null — it never resolves to another limb.
	/// </summary>
	internal static Limb? ResolveNamed(Body body, int requestedLimbIndex)
	{
		if (requestedLimbIndex >= 0
			&& requestedLimbIndex < body.limbs.Length
			&& body.limbs[requestedLimbIndex] != null // Unity object — ==
			&& !body.limbs[requestedLimbIndex].dismembered)
		{
			return body.limbs[requestedLimbIndex];
		}

		return null;
	}

	/// <summary>
	/// The limb an AUTO-selectable request lands on: the named one when this body can
	/// serve it, otherwise the most injured attached limb. Deliberately NOT the
	/// limb-tool family's question — see the class doc.
	/// </summary>
	internal static Limb? Resolve(Body body, int requestedLimbIndex) =>
		ResolveNamed(body, requestedLimbIndex) ?? PickMostInjured(body);

	/// <summary>The most injured attached limb of this body, or nothing when it has none.</summary>
	private static Limb? PickMostInjured(Body body)
	{
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
