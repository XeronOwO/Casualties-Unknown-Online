namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One placement resolution: accepted at a body slot (<see cref="Slot"/> &gt;= 0), accepted inside a container
/// (<see cref="ContainerId"/> != 0), or refused with the receiving rules' own reason. The host records exactly
/// this destination — the receiving client's answer, never the giver's request.
/// </summary>
public readonly record struct RemotePlacementResolution(bool Accepted, int Slot, ulong ContainerId, string Reason)
{
	public static RemotePlacementResolution ToSlot(int slot) => new(true, slot, 0, string.Empty);

	public static RemotePlacementResolution IntoContainer(ulong containerId) => new(true, -1, containerId, string.Empty);

	public static RemotePlacementResolution Refused(string reason) => new(false, -1, 0, reason);
}
