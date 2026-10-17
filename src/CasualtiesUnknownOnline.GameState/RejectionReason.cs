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

	/// <summary>
	/// The report names an item that the sender's own carried-set statement did not
	/// claim, and this host has already judged that statement — so the report is not
	/// the item's creation claim and nothing about it can be committed. It is
	/// answered by name: the sender settles it by RE-STATING the set (the absolute
	/// report), which is how an item it really carries reaches this host.
	/// </summary>
	UnclaimedItem,

	/// <summary>
	/// A native-use report names an item whose OWN data says no native use exists
	/// (<c>ItemInfo.usable</c> is clear, so both of the game's use call sites fall
	/// through to something else), which no client of this game could have
	/// performed. It is answered by name: the sender has already applied the use
	/// locally by the time the report arrives, and the recorded state is written
	/// back onto that object (<c>ItemStateSettlement</c>) so the two sides agree
	/// again.
	/// </summary>
	UseNotPermitted,

	/// <summary>
	/// The host's cheat-permission roster does not name this member, so the cheat
	/// verbs (<c>/data</c>'s write verbs today, the rest of the family as it lands)
	/// are closed to it. It is the SECOND of the host's two cheat rulings: the family
	/// must also be open, which is refused as <see cref="HostRuleDenied"/> and judged
	/// first — so a member needs the host's switch AND its grant. It is answered to the
	/// sender by name because a console write has already changed the sender's local
	/// object and the sender settles that change back on receipt.
	/// </summary>
	CheatPermissionDenied,
}
