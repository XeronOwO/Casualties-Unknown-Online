using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read seam's half of the null-collection rule: <b>null means none</b>.
///
/// A declaration's collection member is read through these helpers rather than
/// dereferenced, because the two shapes that carry a declaration answer for null
/// differently and both must mean the same thing to a consumer. The framework's
/// ready-made data classes cannot produce null at all — their setters coalesce a
/// null write into an empty collection — while a mod-authored implementation
/// computes its members and may hand back null for one it does not carry. A
/// consumer that dereferenced the second shape would throw inside a provider,
/// where nothing per-definition can catch it, and the definitions ordered after
/// it would never materialize; a consumer that asks here gets an empty
/// collection and the rule decision 244 already stated.
///
/// This is a read, not a repair: the declaration object is never written to, so
/// a mod that hands out null keeps handing out null.
/// </summary>
public static class ModDeclarationCollections
{
	/// <summary>The declaration's list, or an empty one when it carries none.</summary>
	public static List<T> OrEmpty<T>(List<T>? values) => values ?? [];

	/// <summary>The declaration's dictionary, or an empty one when it carries none.</summary>
	public static Dictionary<TKey, TValue> OrEmpty<TKey, TValue>(Dictionary<TKey, TValue>? values)
		where TKey : notnull => values ?? [];
}
