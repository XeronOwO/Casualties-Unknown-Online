namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What one load found: the state it settled on, the manifest that passed the
/// gate, the snapshot content, and every reason something was lost. The caller
/// must surface <see cref="DamageReport"/> (in-game per §6) — the report is not
/// advisory, it is the only account of what did not load.
/// </summary>
public sealed record WorldLoadResult(
	WorldLoadState State,
	string WorldId,
	WorldSnapshotContent? Content,
	DamageReport Report,
	string Summary)
{
	/// <summary>True = a snapshot was opened; content may still be damaged or salvaged.</summary>
	public bool Loaded => Content is not null;

	/// <summary>True = the live snapshot could not be opened at all.</summary>
	public bool Failed => Content is null;
}
