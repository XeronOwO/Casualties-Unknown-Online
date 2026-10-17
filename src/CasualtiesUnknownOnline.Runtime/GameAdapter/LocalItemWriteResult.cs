namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What became of one <see cref="ILocalItemAccess.WriteLocalItem"/> call. The caller
/// answers the operator by name, so every refusal is a value rather than a silent false:
/// "this client does not hold it", "the local object is not this side's fact" and "the
/// document is not a state this item can carry" are different facts to the person who
/// typed the command.
/// </summary>
public enum LocalItemWriteResult
{
	/// <summary>The local object now carries the state the document described.</summary>
	Applied,

	/// <summary>This client holds no item under that id.</summary>
	NotHeld,

	/// <summary>
	/// The id resolves to a local object whose state is NOT this client's fact — a
	/// display proxy, or another member's carried item incarnated locally as a standing
	/// object — so writing it would report somebody else's item as this side's own.
	/// </summary>
	NotWritable,

	/// <summary>
	/// The local object is this client's, but the document is not a state it can carry
	/// (an unknown key, a component it does not have, a value its field's kind cannot
	/// take, more liquid than the container holds). Nothing was written; the adapter's
	/// sentence names the ground.
	/// </summary>
	Refused,

	/// <summary>
	/// This build cannot express an item state at all (no Game Adapter, so no language/schema),
	/// so the recorded state could not be named to put back. Distinct from <see cref="NotHeld"/>:
	/// the item may be held; the vocabulary is what is missing.
	/// </summary>
	Unavailable,
}
