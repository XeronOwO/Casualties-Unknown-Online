namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The language surface of <see cref="IModContext"/>: a mod ships its own sentences so what a PLAYER reads is rendered by the client that reads it.
/// </summary>
/// <remarks>
/// Registering a catalogue is additive and never redefines the framework's own text — an entry the framework already has wins, so a mod cannot rename a
/// built-in answer by accident. Every key a mod registers must live under the mod's own namespace (<c>&lt;mod id&gt;.</c>), which is what makes two mods'
/// catalogues safe to merge and lets the gate tell a mod's key from a framework key.
/// </remarks>
public interface IModLocalisation
{
	/// <summary>
	/// Registers (or extends) one language table, in the built-in flat JSON format (<c>{ "key": "sentence {0}", … }</c>). A malformed catalogue, or one whose
	/// keys are outside this mod's namespace, is refused as a whole and reported in the framework log; a second call for the same language merges the new
	/// entries the same way.
	/// </summary>
	bool RegisterCatalog(string language, string json);
}
