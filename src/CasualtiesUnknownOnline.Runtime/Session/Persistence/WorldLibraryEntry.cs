using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One row of the world library (decision 198): the world's own picker row —
/// its id, display name, last write, kind, layer and member count — plus the two
/// facts the page decides with (<see cref="HasSnapshot"/> and
/// <see cref="BackupCount"/>) and whether the Continue entry currently opens it.
///
/// <see cref="IsSelected"/> is deliberately read from the entry's own answer rather
/// than re-derived here: the page marks a world and the native Load button opens one,
/// and those two must never disagree.
/// </summary>
public sealed record WorldLibraryEntry(
	WorldIndex.WorldEntry World,
	bool HasSnapshot,
	int BackupCount,
	bool IsSelected);
