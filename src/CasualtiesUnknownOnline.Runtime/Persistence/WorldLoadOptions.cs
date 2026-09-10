namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// How permissive a load is. S1 ships the contract; S2 decides what the game
/// path passes. <see cref="RepairMode"/> is the production default (§6): salvage
/// per entry, never a silent restart of the layer.
/// </summary>
public sealed class WorldLoadOptions
{
	/// <summary>False = a damaged file aborts the load instead of being skipped. Used by strict tooling/tests.</summary>
	public bool RepairMode { get; init; } = true;

	/// <summary>
	/// True = re-read every listed file and compare it with the manifest before returning.
	/// Only the caller that is about to APPLY the payload needs this (S2's restore path);
	/// a cheap listing does not. A manifest declaring <see cref="ChecksumPolicy.None"/>
	/// overrides this — its digests are informational (§3.2).
	/// </summary>
	public bool VerifyChecksums { get; init; }

	/// <summary>The schema version this caller understands; defaults to <see cref="SaveManifest.CurrentSchemaVersion"/>.</summary>
	public int ReaderSchemaVersion { get; init; } = SaveManifest.CurrentSchemaVersion;
}
