namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 52; // v52: enemy combat result wires removed (bite/lunge/effect ride KernelEnvelope)

}
