namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the terminal transition reason.
/// </summary>
public enum WireTerminalKind
{
	Consumed = 1,
	Destroyed = 2,
	ReplacedBy = 3,
}
