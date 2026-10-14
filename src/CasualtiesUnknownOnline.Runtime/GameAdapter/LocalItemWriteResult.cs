namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What became of one <see cref="ILocalItemAccess.WriteLocalItem"/> call. The
/// caller answers the operator by name, so every refusal is a value rather than a
/// silent false: "this client does not hold it" and "the local object is not this
/// side's fact" are different facts to the person who typed the command.
/// </summary>
public enum LocalItemWriteResult
{
	/// <summary>The local object now carries the state the caller computed.</summary>
	Applied,

	/// <summary>This client holds no item under that id.</summary>
	NotHeld,

	/// <summary>
	/// The id resolves to a local object whose state is NOT this client's fact — a
	/// display proxy, or another member's carried item incarnated locally as a
	/// standing object — so writing it would report somebody else's item as this
	/// side's own.
	/// </summary>
	NotWritable,
}
