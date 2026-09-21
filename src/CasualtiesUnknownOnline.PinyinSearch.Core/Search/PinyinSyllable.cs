namespace CasualtiesUnknownOnline.PinyinSearch.Core.Search;

/// <summary>
/// One reading of a hanzi as the matcher consumes it: the initial and final
/// alternates (the fuzzy-sound substitutions expanded at load time) plus the
/// tone digit. Ported from the standalone JustUnknownCharacters mod, whose
/// matcher and reading table come from PinIn
/// (<see href="https://github.com/Towdium/PinIn"/>).
/// </summary>
internal sealed class PinyinSyllable(string[] initials, string[] finals, string tone)
{
	/// <summary>The initial alternates including the fuzzy ones (for example ["zh", "z"]); empty for a vowel-initial reading.</summary>
	public string[] Initials { get; } = initials;

	/// <summary>The final alternates including the fuzzy ones (for example ["eng", "en"]); empty when the reading has no final.</summary>
	public string[] Finals { get; } = finals;

	/// <summary>The tone digit "1".."5" as it appears in the reading table.</summary>
	public string Tone { get; } = tone;
}
