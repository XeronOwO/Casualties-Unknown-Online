namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Authoritative durable player skill facts in the kernel. Skills are not
/// high-frequency stream fields: they change through permanent progression or
/// run rules (respawn keep/reset), so they belong to the player domain and
/// ride checkpoint/wire/save like the other durable player facts.
/// </summary>
public sealed record PlayerSkillsState(
	int Strength,
	int Resistance,
	int Intelligence,
	float ExpStrength,
	float ExpResistance,
	float ExpIntelligence);
