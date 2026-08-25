using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Thin façade over the body-component sync helpers that Mapster cannot see
/// (<see cref="PainkillersSync"/> and <see cref="MedicationComponentsSync"/>).
/// Keeps the character-data capture/apply call sites to one line each; pure
/// static dispatch, no state.
/// </summary>
internal static class CharacterComponentSync
{
	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		PainkillersSync.Capture(body, health);
		MedicationComponentsSync.Capture(body, health);
	}

	internal static void Apply(Body body, CharacterHealthMsg? health)
	{
		PainkillersSync.Apply(body, health);
		MedicationComponentsSync.Apply(body, health);
	}
}
