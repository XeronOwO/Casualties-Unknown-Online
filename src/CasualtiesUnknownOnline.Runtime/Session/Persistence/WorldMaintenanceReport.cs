using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// One world-library action's outcome (decision 198): what was asked, whether it
/// happened, and every line the account of it carries.
///
/// It exists because both surfaces that can show it — the Worlds page in the
/// Online UI and the log — must be able to say WHY nothing happened. A refused
/// restore is the case that matters: the player clicked, the world folder is
/// untouched, and "nothing happened" is exactly what they cannot diagnose.
/// </summary>
public sealed record WorldMaintenanceReport(
	WorldMaintenanceKind Kind,
	bool Succeeded,
	string WorldId,
	string? BackupFileName,
	string Detail,
	IReadOnlyList<string> Account)
{
	/// <summary>A refused action: nothing was written, and <paramref name="detail"/> says why.</summary>
	internal static WorldMaintenanceReport Refused(WorldMaintenanceKind kind, string worldId, WorldBackup? backup, string detail) =>
		new(kind, false, worldId, backup?.FileName, detail, []);

	/// <summary>A completed action, with the lines its account carries.</summary>
	internal static WorldMaintenanceReport Completed(WorldMaintenanceKind kind, string worldId, WorldBackup? backup, string detail, IReadOnlyList<string> account) =>
		new(kind, true, worldId, backup?.FileName, detail, account);

	/// <summary>One line for the log: the kind, the world, the archive when there is one, and the outcome.</summary>
	public string Describe() =>
		$"{Kind} of world {WorldId}{(BackupFileName is { Length: > 0 } backup ? $" from backup {backup}" : string.Empty)} " +
		$"{(Succeeded ? "completed" : "was refused")}: {Detail}";
}
