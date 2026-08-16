namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The pure limb-presentation formulas the remote clone renderer feeds with
/// the owner's synced limb values (no Unity — L0-lockable). The formulas
/// mirror the game's own visual code exactly:
/// - skin/muscle damage + infection tint: Limb.Update, Limb.cs:501-503
/// - blood drip emission: FurBloodUpdate emits at rate 5 only while
///   furBloodAmount is above 0.95 (Limb.cs:463-471; the downward-transfer and
///   water branches are owner-side continuous simulation — the clone applies
///   the snapshot's terminal furBlood amount, so only the threshold branch
///   is replicated here).
/// - active-state toggle: a dismembered limb is SetActive(false)
///   (Limb.cs:115-116/139/186-188); the clone's Instantiate copies the
///   template's active state, so the renderer must apply in both directions.
/// </summary>
internal static class LimbPresentation
{
	/// <summary>The fur-blood amount above which the game's own bleed drip starts (Limb.cs:463).</summary>
	internal const float BloodDripThreshold = 0.95f;

	/// <summary>The game's drip emission rate above the threshold (Limb.cs:465).</summary>
	internal const float BloodDripRate = 5f;

	internal static float SkinDamage(float skinHealth) => 100f - skinHealth;

	internal static float MuscleDamage(float muscleHealth) => 100f - muscleHealth;

	internal static float InfectionPercent(float infectionAmount) => infectionAmount * 0.01f;

	internal static float PainAmount(float pain, float adrenaline) =>
		Clamp01(pain * 0.01f - adrenaline * 0.005f);

	internal static float SnowAmount(float snowAmount, float dirtyness) =>
		Max(snowAmount, dirtyness * 0.01f);

	internal static float DirtynessAmount(float dirtyness) => Clamp01(dirtyness * 0.02f);

	internal static float WetnessAmount(float wetness) => wetness * 0.01f;

	internal static float BloodEmissionRate(float furBloodAmount) =>
		furBloodAmount > BloodDripThreshold ? BloodDripRate : 0f;

	internal static bool MustSetActive(bool dismembered, bool currentlyActive) => currentlyActive == dismembered;

	private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

	private static float Max(float a, float b) => a > b ? a : b;
}
