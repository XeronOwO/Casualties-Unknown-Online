namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What one cut write resolved to. A refusal carries the reason the trigger's
/// caller reports (the console line and the log both show it); a success
/// carries the numbers the report names, so nothing has to re-read the archive
/// to say what was stored.
/// </summary>
/// <param name="Success">True = a snapshot was committed.</param>
/// <param name="Detail">The refusal reason ("" on success).</param>
/// <param name="Revision">The kernel revision the cut froze.</param>
/// <param name="Layer">The run's layer index at the cut instant (-1 when unknown).</param>
/// <param name="Files">Payload files written.</param>
/// <param name="BlockRows">World-block rows (the block diff plus the game's own damage rows).</param>
/// <param name="TransientRows">World-transient rows.</param>
/// <param name="BackupPath">The backup archive the transaction produced.</param>
internal sealed record WorldCutWriteResult(
	bool Success,
	string Detail,
	ulong Revision,
	int Layer,
	int Files,
	int BlockRows,
	int TransientRows,
	string BackupPath)
{
	internal static WorldCutWriteResult Refused(string detail) => new(false, detail, 0, -1, 0, 0, 0, string.Empty);

	internal static WorldCutWriteResult Captured(
		ulong revision, int layer, int files, int blockRows, int transientRows, string backupPath) =>
		new(true, string.Empty, revision, layer, files, blockRows, transientRows, backupPath);
}
