namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 32; // v32: CharacterHealthMsg carries remote-clone FacialExpression latches (disfigured index + heal presentation timers); v31 peers cannot render a remote player's disfigurement/eye-loss face state

}
