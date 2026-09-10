namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What triggered a cut, as the manifest's <c>saveReason</c> records it (§3.2).
/// The reason is provenance, not policy: several reasons can produce the same
/// kind of cut, and the restore path treats them identically.
/// </summary>
public enum WorldCutReason
{
	/// <summary>The host crossed a layer boundary — the layer-end capture S2 ships.</summary>
	LayerAdvance,

	/// <summary>The host deliberately left the world for the main menu.</summary>
	MenuReturn,

	/// <summary>An explicit player command (S4's management surface).</summary>
	Command,

	/// <summary>The configurable interval autosave (S4).</summary>
	AutoInterval,

	/// <summary>The safety copy taken before a restore (S4's <c>pre-restore-backup</c>).</summary>
	PreRestoreBackup,
}
