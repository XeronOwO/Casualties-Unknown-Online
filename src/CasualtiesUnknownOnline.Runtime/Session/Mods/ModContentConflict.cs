using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One content-catalog conflict. It is a read-only diagnostic record: future
/// content binding can consume it to decide whether to refuse a lazy bind,
/// log a warning, or require a mod coordination path.
/// </summary>
public sealed record ModContentConflict(
	string Kind,
	string Id,
	ModContentConflictKind ConflictKind,
	IReadOnlyList<string> OwnerModIds,
	string Reason);
