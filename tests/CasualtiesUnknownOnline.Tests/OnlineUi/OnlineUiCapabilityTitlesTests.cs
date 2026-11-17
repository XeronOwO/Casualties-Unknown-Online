using System;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Capabilities;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The refusal line's wording: the port carries capability ids, the window turns
/// each one into a catalogue key, and every id the catalog declares must answer a
/// key of its own — an unmapped id would read the sentence with a machine name or
/// silently fall back to the unclassified system.
///
/// <para>
/// The keys themselves are checked by the catalogue gates (both languages declare
/// them and the product source reads each literal); this table pins the mapping.
/// </para>
/// </summary>
public class OnlineUiCapabilityTitlesTests
{
	[Fact]
	public void EveryDeclaredCapability_HasAKeyOfItsOwn()
	{
		var unmapped = AdapterCapabilityCatalog.All
			.Select(definition => definition.Id)
			.Where(id => string.Equals(
				OnlineUiCapabilityTitles.KeyFor(id),
				OnlineUiCapabilityTitles.KeyFor(OnlineUiCapabilityTitles.UnclaimedId),
				StringComparison.Ordinal))
			.ToList();

		Assert.Empty(unmapped);
	}

	[Fact]
	public void DistinctIds_AnswerDistinctKeys()
	{
		var keys = AdapterCapabilityCatalog.All
			.Select(definition => OnlineUiCapabilityTitles.KeyFor(definition.Id))
			.ToList();

		Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
	}

	[Fact]
	public void AnUnknownId_ReadsAsTheUnclassifiedSystem()
	{
		Assert.Equal(
			OnlineUiCapabilityTitles.KeyFor(OnlineUiCapabilityTitles.UnclaimedId),
			OnlineUiCapabilityTitles.KeyFor("a-capability-from-a-newer-build"));
	}
}
