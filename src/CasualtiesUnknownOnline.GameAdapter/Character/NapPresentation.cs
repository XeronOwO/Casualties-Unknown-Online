namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Pure translation of the wire nap-variant byte to the animator clip pair the
/// game uses. The game stores no public "which nap coroutine ran" field; the
/// two coroutines are captured by <see cref="Patches.BodyNapPatch"/> into a
/// tiny local tracker, and this mapper lets the render-clone replay have an
/// L0 test face without touching an Animator.
/// </summary>
internal static class NapPresentation
{
	/// <summary>Wire value for the standard <c>NapCoroutine</c> lay-down clips.</summary>
	internal const byte Normal = 0;

	/// <summary>Wire value for the sick <c>AltNapCoroutine</c> lay-down clips.</summary>
	internal const byte Alt = 1;

	internal static string BodyClip(byte napVariant) =>
		napVariant == Alt ? "ExperimentLayDownAlt" : "ExperimentLayDown";

	internal static string ArmsClip(byte napVariant) =>
		napVariant == Alt ? "ArmsLayDownAlt" : "ArmsLayDown";
}
