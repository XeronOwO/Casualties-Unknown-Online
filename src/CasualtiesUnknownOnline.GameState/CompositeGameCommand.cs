using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// A host-only composite command that atomically executes several typed domain
/// commands as one kernel batch. Inner commands are decided and reduced in
/// declaration order on the same working copy, so a later command can observe
/// an earlier command's staged result. If any inner command is rejected the
/// whole composite is rejected; otherwise all emitted events are reduced under
/// one global revision. Only the composite's OperationId is an idempotency key;
/// inner OperationIds are not recorded in the kernel operation window.
/// </summary>
public sealed record CompositeGameCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	IReadOnlyList<GameCommand> Commands) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
