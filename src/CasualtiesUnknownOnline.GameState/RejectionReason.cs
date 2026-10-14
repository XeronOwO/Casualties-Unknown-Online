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

	/// <summary>
	/// The host's own rule does not open this verb to members (the console
	/// item-data write today): the report is a fact the host could have committed,
	/// and the refusal is the host's policy rather than the report's shape. It is
	/// answered to the sender by name, because a console write has already changed
	/// the sender's local object and the sender settles that change back on
	/// receipt.
	/// </summary>
	HostRuleDenied,
}
