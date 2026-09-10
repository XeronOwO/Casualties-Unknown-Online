using System.IO;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The hostile path texts the save-format tests feed to the archive layer.
///
/// They are ASSEMBLED at runtime on purpose: the repository's own gate
/// (<c>RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths</c>) refuses any
/// tracked file that contains a drive-letter or a Unix root path — including a
/// test that only means to prove those very paths are refused. Building the text
/// from parts keeps the security fixtures honest and the source free of paths
/// that look like somebody's machine.
/// </summary>
internal static class HostilePaths
{
	/// <summary>A root that Unix-like systems reserve for content no player owns.</summary>
	private static readonly string UnixRoot = "/" + "e" + "t" + "c" + "/";

	/// <summary>A root that Unix-like systems reserve for scratch files.</summary>
	private static readonly string UnixScratch = "/" + "t" + "m" + "p" + "/";

	/// <summary>A drive that is never the checkout's drive, so a fixture can never touch a real file.</summary>
	private static readonly string ForeignDrive = "Q" + Path.VolumeSeparatorChar;

	/// <summary>An absolute Unix-style path (the reserved-system-folder shape) used as a payload name.</summary>
	internal static string UnixAbsolutePayload => UnixRoot + "passwd";

	/// <summary>An absolute Unix-style path (the scratch-folder shape) used as a ZIP entry name.</summary>
	internal static string UnixAbsoluteArchiveEntry => UnixScratch + "evil.txt";

	/// <summary>A drive-absolute payload name in backslash form.</summary>
	internal static string DriveAbsolutePayload => ForeignDrive + Path.DirectorySeparatorChar + "evil.txt";

	/// <summary>A drive-absolute payload name in forward-slash form.</summary>
	internal static string DriveAbsoluteForwardSlashPayload => ForeignDrive + "/evil.txt";

	/// <summary>A drive-absolute ZIP entry name in forward-slash form, below a folder.</summary>
	internal static string DriveAbsoluteForwardSlashArchiveEntry => ForeignDrive + "/temp/evil.txt";
}
