namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One injectable/IV medicine liquid's host-authoritative cross-player effect,
/// expressed per millilitre (the game's <c>WaterContainerItem.Inject</c>
/// consumes variable ml per container). The values mirror the
/// <c>LiquidType.onHealthUse</c> delegates in Liquids.cs for the curated
/// medicine slice. Timed/random onHealthUse branches carry their native
/// <c>TimedEffectId</c> + per-ml duration and run on the target's local body;
/// opiate and opiate-antagonist components are also included. Pure data — no
/// game assembly dependency, no state.
/// </summary>
public sealed record RemoteMedicineLiquidEffect(
	string LiquidId,
	float BloodVolumePerMl = 0f,
	float BloodViscosityPerMl = 0f,
	float ThirstPerMl = 0f,
	float SicknessPerMl = 0f,
	float SepticShockPerMl = 0f,
	float AntibioticImmunityTimePerMl = 0f,
	float BloodOxygenPerMl = 0f,
	float RespiratoryRatePerMl = 0f,
	float StaminaPerMl = 0f,
	float FibrillationProgressPerMl = 0f,
	float StrokeAmountPerMl = 0f,
	float AdrenalinePerMl = 0f,
	float PainPerMl = 0f,
	float MuscleHealthPerMl = 0f,
	float DisinfectionTimePerMl = 0f,
	float SkinHealAmountPerMl = 0f,
	float BleedAmountPerMl = 0f,
	float SkinHealthPerMl = 0f,
	float OpiateAmountPerMl = 0f,
	float AntagonistAmountPerMl = 0f,
	string? TimedEffectId = null,
	float TimedDurationPerMl = 0f);
