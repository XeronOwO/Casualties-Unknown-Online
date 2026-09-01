using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CommandSelectorSuggestionsTests
{
	[Fact]
	public void BaseSelectorSuggestions_IncludeAllSelectorsForAtPrefix()
	{
		var suggestions = CommandSelectorSuggestions.Suggest("@");

		Assert.Contains(suggestions, s => s.Text == "@a");
		Assert.Contains(suggestions, s => s.Text == "@p");
		Assert.Contains(suggestions, s => s.Text == "@s");
		Assert.Contains(suggestions, s => s.Text == "@e");
		Assert.Contains(suggestions, s => s.Text == "@r");
	}

	[Fact]
	public void FilterKeySuggestions_AreFullSelectorPrefixes()
	{
		var suggestions = CommandSelectorSuggestions.Suggest("@a[");

		Assert.Contains(suggestions, s => s.Text == "@a[type=");
		Assert.Contains(suggestions, s => s.Text == "@a[name=");
		Assert.Contains(suggestions, s => s.Text == "@a[distance=");
		Assert.Contains(suggestions, s => s.Text == "@a[limit=");
		Assert.Contains(suggestions, s => s.Text == "@a[sort=");
	}

	[Fact]
	public void TypeValueSuggestions_AreFullSelectorPrefixes()
	{
		var suggestions = CommandSelectorSuggestions.Suggest("@a[type=");

		Assert.Contains(suggestions, s => s.Text == "@a[type=player");
		Assert.Contains(suggestions, s => s.Text == "@a[type=cuo:player");
	}

	[Fact]
	public void SortValueSuggestions_AreFullSelectorPrefixes()
	{
		var suggestions = CommandSelectorSuggestions.Suggest("@a[sort=n");

		Assert.Contains(suggestions, s => s.Text == "@a[sort=nearest");
	}
}
