namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One drinkable medicine liquid's host-authoritative cross-player effect,
/// expressed per millilitre. The values mirror the <c>LiquidType.onDrink</c>
/// delegates in Liquids.cs for the curated drinkable-medicine slice. Timed and
/// random branches are not simulated by the host: they travel as a
/// <c>TimedBodyEffectMsg</c> with the exact drawn dose so the target's local
/// body runs the native per-tick/one-shot behaviour. Pure data — no game
/// assembly dependency, no state.
/// </summary>
public sealed record RemoteDrinkMedicineEffect(
	string LiquidId,
	float SicknessPerMl = 0f,
	float HappinessPerMl = 0f,
	float SepticShockPerMl = 0f,
	float AntibioticImmunityTimePerMl = 0f,
	float BloodPressureChangeFromMedicinePerMl = 0f,
	float ClawRegrowTimePerMl = 0f,
	float ClawRegrowOverdoseTimePerMl = 0f,
	float ClawRegrowOverdoseSicknessPerMl = 0f,
	float OpiateAmountPerMl = 0f,
	float AntagonistAmountPerMl = 0f,
	float OpiateTolerancePerMl = 0f,
	float SleepingPillsAmountPerMl = 0f,
	float ShockPerMl = 0f,
	float BrainGrowSicknessPerMl = 0f,
	float BrainGrowMindwipeThresholdMl = 0f,
	bool TriggersMindwipe = false,
	string? TimedEffectId = null,
	float TimedDurationPerMl = 0f,
	float TimedDurationSeconds = 0f);
