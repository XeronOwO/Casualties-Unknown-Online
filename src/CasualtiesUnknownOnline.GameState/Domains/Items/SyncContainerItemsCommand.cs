using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Reconcile one container subtree in a single atomic batch: the parent fact
/// update plus create/update/move/destroy of every contained child. Container
/// contents are authoritative discrete state, so this is a reliable kernel
/// command, never a state stream.
/// </summary>
public sealed record SyncContainerItemsCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ItemIdentity ParentIdentity,
	ItemData ParentData,
	IReadOnlyList<ContainerChildFact> Children) : GameCommand(
		OperationId,
		Actor,
		RunEpoch,
		Authority,
		[new ExpectedRevision(ParentIdentity.InstanceId, 0)]);
