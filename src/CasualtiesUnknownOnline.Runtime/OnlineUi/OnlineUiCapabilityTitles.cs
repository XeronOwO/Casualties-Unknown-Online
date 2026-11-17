namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The catalogue keys behind the capability refusal line's system names.
///
/// <para>
/// The adapter's capability ids are stable and language-free; the sentence that
/// names them belongs to the window, so the port carries ids and this map turns
/// one into the key the reader's own catalogue answers. A sentence that
/// interpolated the catalogue's English titles read as English inside Chinese —
/// measured 2026-11-18 on the real game.
/// </para>
/// </summary>
public static class OnlineUiCapabilityTitles
{
	/// <summary>The id the refusal line uses for a failed hook no capability claims (a catalogue gap, still refused as a whole).</summary>
	public const string UnclaimedId = "unclaimed";

	private const string Session = "session";
	private const string World = "world";
	private const string Items = "items";
	private const string Traps = "traps";
	private const string Medical = "medical";
	private const string Character = "character";
	private const string Enemies = "enemies";
	private const string Crafting = "crafting";
	private const string Trading = "trading";
	private const string Save = "save";
	private const string Tutorial = "tutorial";
	private const string Mods = "mods";
	private const string Diagnostics = "diagnostics";

	/// <summary>The catalogue key for a capability id; an id this build does not know reads as the unclassified system rather than as a machine name.</summary>
	public static string KeyFor(string capabilityId)
	{
		return capabilityId switch
		{
			Session => "capability.session.title",
			World => "capability.world.title",
			Items => "capability.items.title",
			Traps => "capability.traps.title",
			Medical => "capability.medical.title",
			Character => "capability.character.title",
			Enemies => "capability.enemies.title",
			Crafting => "capability.crafting.title",
			Trading => "capability.trading.title",
			Save => "capability.save.title",
			Tutorial => "capability.tutorial.title",
			Mods => "capability.mods.title",
			Diagnostics => "capability.diagnostics.title",
			_ => "capability.unclaimed.title",
		};
	}
}
