namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The outcome of a write transaction. A failed write leaves <c>live/</c>
/// exactly as it was (§5) and reports which step refused — never a partial
/// snapshot and never a silent success.
/// </summary>
public sealed record SaveWriteResult(
	bool Success,
	string WorldId,
	SaveManifest? Manifest,
	string? SnapshotDirectory,
	string? BackupArchivePath,
	SaveWriteResult.Failure Reason,
	string Detail)
{
	/// <summary>The transaction step that refused the write.</summary>
	public enum Failure
	{
		None,

		/// <summary>The request itself is unusable (unsafe path, duplicate path, unknown world id).</summary>
		InvalidRequest,

		/// <summary>Staging the payload or the manifest failed — I/O, or the staging folder could not be reset.</summary>
		StageFailed,

		/// <summary>The staged bytes no longer match the manifest (the verification step of §5).</summary>
		VerifyFailed,

		/// <summary>The backup archive could not be produced, so the commit must not happen.</summary>
		BackupFailed,

		/// <summary>The directory swap failed; the previous snapshot was restored.</summary>
		CommitFailed,
	}

	internal static SaveWriteResult Ok(string worldId, SaveManifest manifest, string snapshotDirectory, string backupArchivePath) =>
		new(true, worldId, manifest, snapshotDirectory, backupArchivePath, Failure.None, string.Empty);

	internal static SaveWriteResult Failed(string worldId, Failure reason, string detail) =>
		new(false, worldId, null, null, null, reason, detail);
}
