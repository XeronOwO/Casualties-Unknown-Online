namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The two native character values that live off the body, as ONE read of the live
/// run scene: <c>PlayerCamera.caloriesConsumed</c> and the wound window's four
/// height/age/id/version values (<c>WoundView.cInfo</c>). The happiness history is
/// not part of it — that one lives on the <c>Body</c> the caller already holds, so
/// it travels as its own argument.
///
/// It is a record rather than a tuple so the FOUR values have one named meaning:
/// <c>SetCharDetails</c>'s native order.
/// </summary>
/// <param name="CaloriesConsumed">The camera's per-run calorie counter.</param>
/// <param name="CharacterInfo">The wound window's four values in the native order: height, age, id, version.</param>
internal readonly record struct CharacterFieldRead(int CaloriesConsumed, int[] CharacterInfo);
