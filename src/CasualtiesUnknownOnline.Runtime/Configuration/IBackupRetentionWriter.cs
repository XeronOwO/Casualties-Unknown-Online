namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// Writes the backup ceiling down where it survives a restart — the BepInEx config entry the save
/// policy is bound to. The Runtime's library asks this instead of knowing the config file, because
/// only the host process owns the file and its entries; a composition without a writer refuses the
/// change with that reason rather than pretending the value stuck.
/// </summary>
public interface IBackupRetentionWriter
{
	/// <summary>Writes <paramref name="keep"/> into the config entry; false = refused, and the refusal says why.</summary>
	bool TryWrite(int keep, out string? refusal);
}
