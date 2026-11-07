namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// The dimensions a handshake refusal can name. The CUO wire protocol is the
/// strict boundary (decisions 137/188); the other dimensions answer "which
/// thing was incompatible" so the guest's status line can say it in the
/// player's own words. <see cref="Unknown"/> (0) is never a legal wire value —
/// the guest's shape check rejects it, exactly as the message registry does for
/// every other closed enum.
/// </summary>
public enum HandshakeDimension
{
	Unknown = 0,

	/// <summary>The wire protocol number (`ProtocolVersion.Current`).</summary>
	Protocol = 1,

	/// <summary>The game build identity (the game's own version string).</summary>
	GameBuild = 2,

	/// <summary>The CUO build identity (`BuildIdentity.Of`, `<version>+<commit>`).</summary>
	CuoBuild = 3,

	/// <summary>The kernel/save archive schema (`SaveManifest.CurrentSchemaVersion`).</summary>
	SaveSchema = 4,

	/// <summary>The loaded synchronized mod set (missing, mode or permission difference).</summary>
	ModSet = 5,

	/// <summary>One mod's own network version (SemVer precedence).</summary>
	ModVersion = 6,
}
