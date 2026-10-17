namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The wire values of a medication component read as what the receiver must end up with.
/// Pure, so the rule that was missing — an ABSENT component has to be removed rather than
/// ignored — is a case instead of an <c>if</c> arm somebody has to notice is absent.
/// </summary>
internal static class MedicationComponentPolicy
{
	/// <summary>The mindwipe component is the one that carries its own presence, so all three states reach here.</summary>
	internal static MedicationComponentPresence ForMindwipe(bool present, bool active) =>
		!present ? MedicationComponentPresence.Absent
			: active ? MedicationComponentPresence.Active
			: MedicationComponentPresence.Inactive;

	internal static MedicationComponentPresence ForSleepingPills(float amount) =>
		amount == 0f ? MedicationComponentPresence.Absent : MedicationComponentPresence.Active;

	internal static MedicationComponentPresence ForAntidepressants(float amount, float currentAmount) =>
		amount == 0f && currentAmount == 0f ? MedicationComponentPresence.Absent : MedicationComponentPresence.Active;

	internal static MedicationComponentPresence ForPainkillers(
		float opiateAmount, float opiateTolerance, float opiateReception, float antagonistAmount, float actualOpiateReception) =>
		opiateAmount == 0f
			&& opiateTolerance == 0f
			&& opiateReception == 0f
			&& antagonistAmount == 0f
			&& actualOpiateReception == 0f
				? MedicationComponentPresence.Absent
				: MedicationComponentPresence.Active;
}
