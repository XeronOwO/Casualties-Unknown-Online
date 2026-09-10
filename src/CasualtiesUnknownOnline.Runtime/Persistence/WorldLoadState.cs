namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>How a snapshot was opened (§6): the live folder, a repaired live folder, or a backup fallback.</summary>
public enum WorldLoadState
{
	/// <summary>The live snapshot read and passed the manifest gate.</summary>
	Current,

	/// <summary>The live snapshot read, but a crash leftover was repaired on the way in.</summary>
	RecoveredLive,

	/// <summary>The live snapshot was unusable; a backup archive was opened instead.</summary>
	BackupFallback,

	/// <summary>Neither the live snapshot nor any backup could be opened.</summary>
	Failed,
}
