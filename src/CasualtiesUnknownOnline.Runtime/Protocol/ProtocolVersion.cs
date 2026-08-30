namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 53; // v53: enemy aggregate removal rides KernelEnvelope (NetMsg.EnemyRemoved removed)

}
