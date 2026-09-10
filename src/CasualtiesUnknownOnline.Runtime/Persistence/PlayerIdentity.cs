namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One peer as the save layer needs to see it: the id the session keys on and
/// the display name the transport advertises. Both halves are claims the
/// transport-scoped player key is built from (§2).
/// </summary>
public sealed record PlayerIdentity(ulong PeerId, string DisplayName)
{
	/// <summary>This peer's transport-scoped key inside <paramref name="space"/>.</summary>
	public string KeyIn(PlayerKeySpace space) => PlayerKeyResolution.KeyOf(PeerId, DisplayName, space);
}
