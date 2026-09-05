using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The subset of <see cref="CharacterHealthMsg"/> that drives a remote clone's
/// live <c>FacialExpression</c> sprite choice. A render clone's
/// <c>Body.Update</c> is skipped, so these body values would otherwise stay at
/// the template defaults; the 1 Hz character snapshot is the self-healing
/// carrier and the game's own <c>FacialExpression.Update</c> remains the visual
/// authority. Kept pure and state-free so the field set is L0-locked without
/// Unity.
/// </summary>
internal readonly record struct FacePresentationVitals(
	float Consciousness,
	float Energy,
	float BadSleepAmount,
	float RadiationSickness,
	float Shock,
	float Adrenaline,
	float SicknessAmount,
	float Temperature,
	float InternalBleeding,
	float BloodPressure,
	float Happiness,
	HeadMouthState HeadMouth)
{
	internal static FacePresentationVitals From(CharacterHealthMsg health) => new(
		health.Consciousness,
		health.Energy,
		health.BadSleepAmount,
		health.RadiationSickness,
		health.Shock,
		health.Adrenaline,
		health.SicknessAmount,
		health.Temperature,
		health.InternalBleeding,
		health.BloodPressure,
		health.Happiness,
		health.HeadMouth);
}
