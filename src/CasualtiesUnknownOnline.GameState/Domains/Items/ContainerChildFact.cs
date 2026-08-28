namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// One desired contained child in a container reconciliation command. The child
/// is already a separate kernel item fact; this record carries the facts needed
/// to create/update/move it in one atomic container batch.
/// </summary>
public sealed record ContainerChildFact(
	ulong InstanceId,
	string DefinitionId,
	ulong ParentItemId,
	ItemData Data);
