namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Online UI's read-only window into a remote player's condition: it opens
/// the game's own native WoundView on that player's 1 Hz character snapshot. The
/// view is display-only and never mutates the live render clone or any
/// authoritative state.
/// </summary>
public interface IRemoteMedicalPresentation
{
	/// <summary>
	/// Opens the game's native WoundView medical UI focused on one in-world
	/// remote player's 1 Hz character snapshot. Returns false when no session,
	/// world, or character health snapshot is available yet. The view is
	/// display-only: it uses a dedicated temporary body copy and never mutates
	/// the live remote render clone or any authoritative state.
	/// </summary>
	bool OpenRemoteMedical(ulong targetSteamId, string displayName);
}
