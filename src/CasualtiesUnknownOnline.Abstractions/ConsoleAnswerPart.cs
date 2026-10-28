using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One LINE of a multi-part console answer: a catalogue key plus its ordered arguments.
///
/// <para>
/// A command answer is not always one sentence. <c>/heal @a</c> answers one line per target, and each line must reach the reader in the reader's
/// language — so the answer carries the parts and the console renders each of them through the local catalogue, the same way a host answer that
/// arrives over the wire already does (<c>ConsoleAnswerPartMsg</c>). A client that has no entry for a part falls back to the answer's own text.
/// </para>
/// </summary>
public sealed class ConsoleAnswerPart(string key, params string[] args)
{
	/// <summary>The catalogue key of this line.</summary>
	public string Key { get; } = key;

	/// <summary>The values the key's sentence places, in the order the catalogue places them.</summary>
	public IReadOnlyList<string> Args { get; } = args is null ? [] : [.. args];
}
