using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

public class ItemCapabilityRegistryTests
{
	[Fact]
	public void DefaultRegistry_IsCompleteAndHasCurrentFeatureCapabilities()
	{
		var registry = CasualtiesUnknownOnline.GameAdapter.Items.ItemCapabilityRegistry.CreateDefault();

		registry.AssertComplete();

		Assert.Contains("saved-state", registry.Names);
		Assert.Contains("liquid", registry.Names);
		Assert.Contains("gun", registry.Names);
		Assert.Contains("custom-data", registry.Names);
	}

	[Fact]
	public void DuplicateName_IsRejectedByCompletenessGate()
	{
		var registry = new GameAdapter.Items.ItemCapabilityRegistry(
		[
			new GameAdapter.Items.SavedStateItemCapability(),
			new GameAdapter.Items.SavedStateItemCapability(),
		]);

		var exception = Assert.Throws<System.InvalidOperationException>(() => registry.AssertComplete());
		Assert.Contains("Duplicate", exception.Message);
	}
}
