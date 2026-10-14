namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One parsed item-data target. The selector stays RAW here — expanding it needs
/// the live entity table, which the command handler owns — so the grammar is
/// parsed and pinned without a session.
/// </summary>
internal sealed record ItemDataQuery(
	ItemDataQueryKind Kind,
	string? Selector,
	int Slot,
	ulong InstanceId);
