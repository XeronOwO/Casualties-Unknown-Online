using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemCheckpointStoreTests
{
	[Fact]
	public void SaveAndLoad_RestoresItemFacts()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		authority.ObserveSpawn(1001, 42, "test_item", 1, 2);

		var store = new ItemCheckpointStore(authority);
		store.Save("run");

		authority.ResetForSession();
		Assert.Null(authority.FindItem(42));

		Assert.True(store.TryLoad("run"));
		var item = authority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.World, item.Location.Kind);
		Assert.Equal(42ul, item.Identity.InstanceId);
	}

	[Fact]
	public void MissingSlot_FailsToLoad()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var store = new ItemCheckpointStore(authority);

		Assert.False(store.TryLoad("missing"));
	}
}
