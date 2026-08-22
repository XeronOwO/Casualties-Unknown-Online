namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 29; // v29: TutorialClawStateMsg (NetMsg 104) — host→guest tutorial-claw 20 Hz presentation stream; v28 peers do not render the remote claw flow

}
