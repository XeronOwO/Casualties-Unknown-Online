using System.Linq;
using CasualtiesUnknownOnline.Runtime.Search;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Search;

/// <summary>
/// The vendored hanzi → pinyin reading table: it must be embedded, parsed and
/// fuzzy-expanded, because every matcher answer rests on it and a table that
/// silently failed to embed would look like "pinyin search matches nothing".
/// </summary>
public class PinyinDictionaryTests
{
	/// <summary>The table is vendored whole (26k+ characters); a floor keeps a truncated or missing resource from passing.</summary>
	private const int ReadingTableFloor = 20000;

	[Fact]
	public void Syllables_LoadsTheEmbeddedReadingTable()
	{
		Assert.True(
			PinyinDictionary.Count > ReadingTableFloor,
			$"expected more than {ReadingTableFloor} characters with readings, got {PinyinDictionary.Count}");
	}

	[Fact]
	public void Syllables_CharactersOutsideTheTable_HaveNoReadings()
	{
		Assert.Empty(PinyinDictionary.Syllables('A'));
		Assert.Empty(PinyinDictionary.Syllables('7'));
		Assert.Empty(PinyinDictionary.Syllables('\uE000'));
	}

	[Fact]
	public void Syllables_PolyphonicCharacter_KeepsEveryReading()
	{
		var readings = PinyinDictionary.Syllables('纤');

		Assert.Equal(2, readings.Length);
		Assert.Equal(["xian", "qian"], readings.Select(r => r.Initials[0] + r.Finals[0]));
		Assert.Equal(["1", "4"], readings.Select(r => r.Tone));
	}

	[Fact]
	public void Syllables_FuzzyInitialsAndFinalsAreExpanded()
	{
		var readings = PinyinDictionary.Syllables('生');

		var reading = Assert.Single(readings);
		Assert.Equal(["sh", "s"], reading.Initials);
		Assert.Equal(["eng", "en"], reading.Finals);
		Assert.Equal("1", reading.Tone);
	}

	[Fact]
	public void Syllables_CoverTheChineseNamesTheFeatureIsUsedOn()
	{
		// 纤维绳 — the ticket's own example name.
		foreach (var character in "纤维绳")
		{
			Assert.True(
				PinyinDictionary.Syllables(character).Length > 0,
				$"no reading for '{character}'");
		}
	}
}
