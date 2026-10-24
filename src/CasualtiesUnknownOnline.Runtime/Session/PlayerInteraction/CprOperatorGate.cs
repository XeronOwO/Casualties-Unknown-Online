namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The CPR operator's capability gate (ticket `remote-medical-cpr.md`, stage 4): the reference refuses an operator who
/// cannot push hard enough — `skills.STR * handMaxTotalForce &gt; 6`, where the hand force is the game's own
/// `Body.limbs[i].totalForce` on a hand limb.
///
/// <para>
/// It is evaluated on the OPERATOR's own client, because the quantity it asks about is their own body: the character
/// snapshot does not carry `totalForce`, and the rule that the affected player's client judges their own capability puts
/// this here. The host still owns the patient's effect; this only decides whether the gesture leaves the client at all.
/// </para>
/// </summary>
public static class CprOperatorGate
{
	/// <summary>The reference's own threshold: the product of strength and hand force must EXCEED it.</summary>
	public const float StrengthThreshold = 6f;

	/// <summary>
	/// Whether this operator can compress: strength times the stronger hand's force must be over the threshold. A body with
	/// no hands (zero force both sides) can never pass, which is right — there is nothing to press with.
	/// </summary>
	public static bool CanCompress(float strength, float handMaxTotalForce) =>
		strength * handMaxTotalForce > StrengthThreshold;
}
