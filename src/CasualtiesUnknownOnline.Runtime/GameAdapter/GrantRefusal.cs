using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// A refusal the item-creation port OWNS: the sentence (the host's log and the fallback), the catalogue KEY that names the same sentence, and the
/// values that key places.
///
/// <para>
/// The reason a definition cannot be created is known where the definition is resolved — inside the Game Adapter, which reads the game's own item
/// table — so that is where the key must be chosen. Free English text reaching the console forces it either to print English on a Chinese client or to
/// fall back to the generic "the host's log names the reason" line, and both are wrong for the player who typed the verb (measured 2026-10-28).
/// </para>
/// </summary>
public sealed class GrantRefusal(string text, string key, params string[] args)
{
	/// <summary>The sentence this refusal was built with: the host's log line and the fallback a missing catalogue entry degrades to.</summary>
	public string Text { get; } = text;

	/// <summary>The catalogue key the reader's client renders instead of <see cref="Text"/>.</summary>
	public string Key { get; } = key;

	/// <summary>The values <see cref="Key"/>'s sentence places, in the catalogue's order.</summary>
	public IReadOnlyList<string> Args { get; } = args is null ? [] : [.. args];
}
