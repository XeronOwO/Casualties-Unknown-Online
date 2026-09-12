using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// B4: a restored block row carries its support-loss verdict with it. The row
/// came out of the world archive, so whatever support loss its air write caused
/// happened in the SAVED world — re-settling it on a receiver would kill a
/// building the authority still holds and re-roll drops that are already
/// checkpoint items. The live path keeps the opposite verdict.
/// </summary>
public sealed class WorldRestoreSupportLossTests
{
	[Fact]
	public void RestoredBlockStateRows_ArriveMarkedAsSupportLossSettled()
	{
		var facts = new FakeWorldFactSource();
		var nativeFacts = new FakeNativeWorldFacts();
		var restore = new WorldFactRestore(facts, nativeFacts, new RecordingLogger<WorldFactRestore>());

		var damage = restore.Apply(
			[SaveWorldBlockRow.OfBlockState(3, 4, 0)],
			[],
			new SaveNativeRunFields { SavedRunTime = 12.5f });

		Assert.Empty(damage);
		var cell = Assert.Single(facts.Blocks);
		Assert.Equal(3, cell.X);
		Assert.Equal(4, cell.Y);
		Assert.Equal(0, cell.Block);
		Assert.True(cell.SupportLossSettled);
	}
}
