namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 15; // v15: ItemCook (NetMsg 92) — a v14 peer would never learn the heater conversion and would keep the raw meat while the host has a steak, so mixed-version sessions are refused instead of silently degrading
}
