namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Where a transferred item ACTUALLY landed on this client — the placement's own answer, reported to the host so
/// it can compare it with the row the give's answer made it write. <see cref="Reason"/> is this client's own
/// sentence for a place that differs from the answered one, or for placing nothing at all.
///
/// <para>
/// <see cref="Applied"/> false is the give's residual: the live body could not take the answered destination and
/// this client did not substitute. It is a first-class outcome rather than a separate failure report, because the
/// host treats both arms the same way — a report that does not match the row is refused by name and the row goes
/// back to the giver (the user's 2026-10-22 ruling: the item ends with the giver).
/// </para>
/// </summary>
internal readonly record struct LocalPlacement(bool Applied, int Slot, ulong ContainerInstanceId, string Reason)
{
	/// <summary>The item is in a body slot; <paramref name="reason"/> names why it is not the answered place when it is not.</summary>
	internal static LocalPlacement InSlot(int slot, string reason = "") => new(true, slot, 0, reason);

	/// <summary>The item is inside the container it was sent to.</summary>
	internal static LocalPlacement InContainer(ulong containerInstanceId) => new(true, -1, containerInstanceId, string.Empty);

	/// <summary>Nothing was placed on this client; <paramref name="reason"/> is its own sentence.</summary>
	internal static LocalPlacement NotPlaced(string reason) => new(false, -1, 0, reason);
}
