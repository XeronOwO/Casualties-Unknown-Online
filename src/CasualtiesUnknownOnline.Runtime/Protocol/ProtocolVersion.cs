namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 6; // v6: the trap-layout authority (TrapLayoutSnapshot NetMsg 79). Additive on the wire but BEHAVIORALLY additive: a peer without it keeps its diverged entity layout (the guest's traps sit at the physics-random positions), so mixed-version sessions are refused by the version gate instead of drifting
}
