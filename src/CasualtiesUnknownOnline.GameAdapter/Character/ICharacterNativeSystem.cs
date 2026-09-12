using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The live scene the native character fields are read from and written to,
/// behind a narrow port: the local <c>Body</c> (handed in directly by the caller),
/// the camera (<c>PlayerCamera.main</c>, <c>caloriesConsumed</c>) and the wound
/// window (<c>WoundView.view</c>, <c>cInfo</c> through <c>SetCharDetails</c>).
///
/// The live game reaches the last two only through statics that no test host can
/// materialize, and the game's own types cannot be constructed there either. The
/// port is therefore plain values in and out: the capture/apply rules
/// (<c>CharacterNativeFieldPolicy</c>, Runtime) are fully testable against a fake,
/// and exactly one implementation resolves the statics — the same shape the native
/// world-fact port already uses.
/// </summary>
internal interface ICharacterNativeSystem
{
	/// <summary>One read of the live scene, or null when this scene cannot describe both values (no camera or no wound window yet — nothing to write back to).</summary>
	CharacterFieldRead? Read();

	/// <summary>
	/// Writes the per-run calorie counter the native load wrote
	/// (<c>SaveSystem.cs:438</c>). False = no camera in this scene, so nothing was
	/// written — the caller names that instead of pretending the value landed.
	/// </summary>
	bool TryApplyCalories(int caloriesConsumed);

	/// <summary>
	/// Writes the wound window's character details the way the native load does:
	/// through <c>SetCharDetails</c>, so the window's text and the two statics it
	/// updates move with the value (<c>WoundView.cs:54-68</c>). False = the window is
	/// not there any more, so nothing was written — the caller names that instead of
	/// reporting a write that never happened.
	/// </summary>
	/// <param name="details">Exactly four values in the native order: height, age, id, version.</param>
	bool SetCharacterDetails(IReadOnlyList<int> details);
}
