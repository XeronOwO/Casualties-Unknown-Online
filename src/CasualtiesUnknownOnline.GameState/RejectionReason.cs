namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Typed reasons a command or applied batch can be rejected. Phase A uses the
/// item-domain subset; later domains extend the same vocabulary.
/// </summary>
public enum RejectionReason
{
	UnknownCommand,
	UnknownAggregate,
	WrongEpoch,
	WrongRevision,
	NotAuthorized,
	InvalidTransition,
	InvariantViolation,
	Conflict,
	AlreadyCommitted,
	MalformedCommand,
	BlockAlreadyBroken,
}
