using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Mutable local UI state for the Online UI window. This is presentation state
/// only; it is owned by <see cref="OnlineUiWindow"/> and never crosses into the
/// Runtime or the wire.
/// </summary>
internal sealed class OnlineUiWindowState
{
	internal bool Visible;

	internal OnlineUiPage Page = OnlineUiPage.Home;

	internal string LobbyIdInput = "";

	internal string? Error;

	internal Vector2 Scroll;

	internal bool LogLevelOptionsOpen;

	internal bool LanguageOptionsOpen;

	internal bool ColorOptionsOpen;

	internal string ProfileNameInput = "";

	internal string? ProfileStatus;

	internal string ConsoleInput = "";

	internal bool ProfileStatusIsError;

	internal OnlineUiTransportMode TransportMode = OnlineUiTransportMode.Steam;

	/// <summary>
	/// The Worlds page's rows, and the library revision they were read at. They are read ON DEMAND
	/// rather than every frame: listing worlds enumerates the repository's folders and their backup
	/// files, and an IMGUI draw runs more than once per frame while the page is open. A reload
	/// happens when the library's <see cref="IWorldLibrary.Revision"/> no longer matches
	/// <see cref="SeenRevision"/> — which covers the first draw (the sentinel starts below every
	/// real revision), every committed cut of any trigger, and every management action — and when
	/// Refresh is clicked. A list that is merely left open therefore does not go stale, and a draw
	/// pass touches no directory.
	/// </summary>
	internal List<WorldLibraryEntry> WorldRows = [];

	/// <summary>The <see cref="IWorldLibrary.Revision"/> the rows were read at; -1 = never read.</summary>
	internal int SeenRevision = -1;

	/// <summary>Whose backups <see cref="BackupRows"/> holds, so the page knows whether they still describe the world the player is looking at.</summary>
	internal string BackupRowsWorldId = "";

	internal List<WorldBackup> BackupRows = [];

	/// <summary>The archive a first click picked, waiting for the confirming second click (empty = no confirmation pending).</summary>
	internal string PendingRestoreFile = "";
}
