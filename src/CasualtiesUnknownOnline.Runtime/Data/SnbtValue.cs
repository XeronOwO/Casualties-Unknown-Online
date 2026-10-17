namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// One value in the item state's data language: the stringified-NBT tree the item-data
/// commands read, write and complete.
///
/// <para>
/// The tree is deliberately small — compound, list, string, integer, float and boolean —
/// because the item's state is small: the save-shaped scalar, liquid and component facts and
/// the captured component fields. Typed arrays, NBT operations and the binary tag set are
/// refused by the parser rather than modelled here, so a value that reaches this type is a
/// value the adapter can bind.
/// </para>
///
/// <para>
/// Values are immutable. Every edit rebuilds the spine it touches and returns the new root
/// (<see cref="SnbtTree"/>), which is what makes a multi-key command atomic: the command edits
/// a tree, binds it once, and a refusal leaves the old tree untouched.
/// </para>
/// </summary>
public abstract record SnbtValue
{
	/// <summary>The word an answer and a log line use for this value's kind.</summary>
	public abstract string Kind { get; }
}
