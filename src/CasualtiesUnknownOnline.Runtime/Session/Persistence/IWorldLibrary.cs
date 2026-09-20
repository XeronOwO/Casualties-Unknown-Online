using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The world library (decision 198): the worlds a repository root holds, the backup
/// archives each world keeps, which world the Continue entry opens, and a restore that
/// replaces one world's live snapshot with an archive the player picked.
///
/// It is the management half of the save system. The save layer already writes a
/// <c>.cuoz</c> archive on EVERY committed cut (§5/§7) and already recovers a world whose
/// live snapshot cannot be opened; what it had no surface for is a player choosing among
/// those archives — and §2's multi-world repository had no picker at all, so the Continue
/// entry opened whatever was last written.
///
/// A restore is ARMED here and EXECUTED by the frame pump
/// (<see cref="Abstractions.ICuoService.Update"/>). The click
/// that arms it happens inside an IMGUI draw callback, which runs more than once per
/// frame, so it may only record an intent; the promotion itself is a multi-file disk
/// operation that has to happen exactly once, on a frame boundary, and only while no
/// world is loaded — a restore replaces the very folder a loaded world is playing from.
/// </summary>
public interface IWorldLibrary
{
	/// <summary>False when this composition root has no world repository (a Runtime-only build, or one that opted out).</summary>
	bool IsEnabled { get; }

	/// <summary>
	/// The world the Continue entry opens, as the entry itself resolves it (null = nothing
	/// is openable). The page marks this row, so a player never sees one world selected and
	/// gets another.
	/// </summary>
	string? SelectedWorldId { get; }

	/// <summary>The last action's outcome for the page's status line; null = nothing has been asked this session.</summary>
	WorldMaintenanceReport? LastReport { get; }

	/// <summary>True = a restore is armed and waiting for the pump.</summary>
	bool HasArmedRestore { get; }

	/// <summary>
	/// Bumped whenever what this library lists may have changed on disk: every finished cut
	/// attempt (a <c>/save</c>, the interval autosave, a layer boundary, a menu return — the save
	/// layer reports them all) and every management action. A caller that caches rows compares
	/// this number and reloads when it moved, which is what keeps a page from showing the world
	/// as it was when the page was first opened.
	/// </summary>
	int Revision { get; }

	/// <summary>Every world this root holds, in the repository's own order, each with the facts the picker shows.</summary>
	IReadOnlyList<WorldLibraryEntry> ListWorlds();

	/// <summary>The world's backup archives, newest first (§7). An unknown world lists nothing.</summary>
	IReadOnlyList<WorldBackup> ListBackups(string worldId);

	/// <summary>
	/// Make <paramref name="worldId"/> the world the Continue entry opens. False = refused,
	/// and <paramref name="refusal"/> says why in the words the page shows. A world with no
	/// committed snapshot is refused: the entry only opens a world that carries one, so
	/// selecting it would leave the pointer naming a world the Load button skips.
	/// </summary>
	bool TrySelectWorld(string worldId, out string? refusal);

	/// <summary>
	/// Arm a restore of <paramref name="backupFileName"/> over <paramref name="worldId"/>'s live
	/// snapshot; it runs at the next frame boundary and reports through
	/// <see cref="LastReport"/>. False = refused now, and <paramref name="refusal"/> says why.
	///
	/// <paramref name="worldActive"/> is the adapter's answer to "is a world loaded or being
	/// generated right now" — the same fact <c>IWorldSaveControl.TryArmIntervalAutosave</c>
	/// takes, for the same reason (only the adapter can observe the scene). It is asked here
	/// for the player's immediate answer AND again at the pump, because a frame can pass
	/// between the click and the execution.
	///
	/// Arming twice supersedes the earlier request, exactly like an armed cut: one world,
	/// one archive, one restore.
	/// </summary>
	bool TryRequestRestore(string worldId, string backupFileName, bool worldActive, out string? refusal);
}
