namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 45; // v45: spider leg IK targets in EnemyState + bite claw replay on host-ordered remote bites

}
