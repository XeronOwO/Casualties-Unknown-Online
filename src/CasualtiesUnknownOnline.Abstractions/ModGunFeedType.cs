namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Feed type for a custom firearm. Mirrors the vanilla
/// <c>GunScript.FeedType</c> enum.
/// </summary>
public enum ModGunFeedType
{
	/// <summary>Magazine-fed weapon.</summary>
	Mag = 0,

	/// <summary>Direct-loaded weapon.</summary>
	Direct = 1
}
