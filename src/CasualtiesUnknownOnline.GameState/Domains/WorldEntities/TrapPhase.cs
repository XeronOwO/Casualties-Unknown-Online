namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// The discrete lifecycle phase of a trap/mechanism entity. The kernel records
/// observed transitions; the game adapter remains the source of the actual
/// native transition.
/// </summary>
public enum TrapPhase : byte
{
	Armed = 1,
	Warning = 2,
	Triggered = 3,
	Cooldown = 4,
	Disabled = 5,
}
