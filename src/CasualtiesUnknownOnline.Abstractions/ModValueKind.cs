namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The kind of a <see cref="ModValue"/> — the typed data model a mod carries across a CUO surface.
///
/// The set is deliberately small and closed: numbers, text, booleans, lists, maps and exactly ONE explicit
/// binary leaf for data that really is bytes. There is no Null kind, because absence is structural — a
/// <c>false</c> from a <c>Try</c>, a map field that is not there — and a nil element would only give every
/// consumer a second way to say "nothing".
///
/// Integers and numbers are two kinds rather than one because a single <c>double</c> cannot carry a Steam
/// id: an integer above 2^53 would come back as a different number, silently, and the whole point of a typed
/// model is that a value means what it says.
/// </summary>
public enum ModValueKind
{
	/// <summary><c>true</c> or <c>false</c>.</summary>
	Boolean = 0,

	/// <summary>A 64-bit signed integer — ids, counts, amounts.</summary>
	Integer = 1,

	/// <summary>A 64-bit IEEE-754 floating point number — measurements, coefficients.</summary>
	Number = 2,

	/// <summary>UTF-16 text, carried as UTF-8 when it travels.</summary>
	Text = 3,

	/// <summary>The model's one explicit binary leaf: bytes the framework never interprets.</summary>
	Binary = 4,

	/// <summary>An ordered sequence of values.</summary>
	List = 5,

	/// <summary>An unordered set of values addressed by name.</summary>
	Map = 6,
}
