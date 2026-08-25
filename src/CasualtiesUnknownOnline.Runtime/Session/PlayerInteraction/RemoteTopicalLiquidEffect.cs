namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One health-usable topical liquid's host-authoritative cross-player effect,
/// expressed per millilitre for the immediate, snapshot-representable branches
/// of the game's <c>LiquidType.onHealthUse</c> delegates. Timed, random,
/// opiate-component, and presentation-only branches are deliberately outside
/// this slice. Pure data — no game assembly dependency, no state.
/// </summary>
public sealed record RemoteTopicalLiquidEffect(
	string LiquidId,
	float PainPerMl = 0f,
	float MuscleHealthPerMl = 0f,
	float InfectionAmountPerMl = 0f,
	float BandageSlowAmountPerMl = 0f,
	float SkinHealAmountPerMl = 0f,
	float DisinfectionTimePerMl = 0f,
	float BloodViscosityPerMl = 0f,
	float SicknessAmountPerMl = 0f,
	float DirtynessPerMl = 0f,
	float PainMultiplier = 1f,
	float PainMultiplierDoseMl = 0f);
