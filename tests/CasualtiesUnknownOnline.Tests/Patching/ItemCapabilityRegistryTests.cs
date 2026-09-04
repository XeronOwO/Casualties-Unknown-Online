using Xunit;
using CasualtiesUnknownOnline.GameAdapter.Items;
using System;

namespace CasualtiesUnknownOnline.Tests.Patching;

public class ItemCapabilityRegistryTests
{
	[Fact]
	public void DefaultRegistry_IsCompleteAndHasCurrentFeatureCapabilities()
	{
		var registry = ItemCapabilityRegistry.CreateDefault();

		registry.AssertComplete();

		Assert.Contains("saved-state", registry.Names);
		Assert.Contains("liquid", registry.Names);
		Assert.Contains("gun", registry.Names);
		Assert.Contains("custom-data", registry.Names);
	}

	[Fact]
	public void DuplicateName_IsRejectedByCompletenessGate()
	{
		var registry = new ItemCapabilityRegistry(
		[
			new SavedStateItemCapability(),
			new SavedStateItemCapability(),
		]);

		var exception = Assert.Throws<InvalidOperationException>(() => registry.AssertComplete());
		Assert.Contains("Duplicate", exception.Message);
	}
}
