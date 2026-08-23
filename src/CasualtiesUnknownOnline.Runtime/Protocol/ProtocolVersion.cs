namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 34; // v34: CrystalTeleportTriggered (EntityEventKind 33) — a v33 peer would not replay the teleport crystal's observerlaugh/FlashBrief; body teleport already rides the 20 Hz player stream

}
