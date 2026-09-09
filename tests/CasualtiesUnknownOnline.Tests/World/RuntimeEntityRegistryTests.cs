using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host's accepted runtime-created entity table (sync-coverage audit E3):
/// creation key → the relayed creation message. It is the absolute source the
/// world-entry group and the 60 s cycle send, so its contract is
/// upsert-by-creation-key, bounded growth, death removal and a layer/session
/// reset. The wire-level recovery loop is GuestEntityReportRecoveryTests; the
/// role guard and the cap need a real session, so this class uses the three-node
/// simulation host.
/// </summary>
[Trait("Category", "Integration")]
public class RuntimeEntityRegistryTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, string keypadCode = "", ulong creator = 0, uint sequence = 0) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		KeypadCode = keypadCode,
		CreatorSteamId = creator,
		CreationSequence = sequence,
	};

	private static RuntimeEntityKey Key(string id, int x, int y, ulong creator = 0, uint sequence = 0) =>
		new(id, x, y, creator, sequence);

	private static RuntimeEntityRegistry Registry(ItemSimWorld w, int? cap = null)
	{
		var session = w.Host.Services.GetRequiredService<ISessionControl>();
		var sender = w.Host.Services.GetRequiredService<PacketSender>();
		return cap is null
			? new RuntimeEntityRegistry(session, sender)
			: new RuntimeEntityRegistry(session, sender, cap.Value);
	}

	[Fact]
	public void Report_UpsertsByCreationKey_AndKeepsTheLatestPayload()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);

		Assert.True(registry.Report(Creation("keypad", 12f, 34f)));
		Assert.True(registry.Report(Creation("keypad", 12.4f, 34f, keypadCode: "4321"))); // the same creation cell (12, 34)
		Assert.True(registry.Report(Creation("keypad", 13f, 34f))); // the next cell is a distinct creation

		Assert.Equal(2, registry.Count);
		Assert.Contains(registry.Entries, e => e.Id == "keypad" && e.KeypadCode == "4321");
	}

	[Fact]
	public void Report_TwoCreationsOfTheSamePrefabInOneCell_AreTwoRecords()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);

		// Same prefab, same floored cell (5, 7), 0.7 m apart: two distinct
		// creations. Only the creation-instance token separates them.
		Assert.True(registry.Report(Creation("turret", 5.2f, 7.2f, creator: 2001, sequence: 1)));
		Assert.True(registry.Report(Creation("turret", 5.9f, 7.6f, creator: 2001, sequence: 2)));

		Assert.Equal(2, registry.Count);
	}

	[Fact]
	public void Report_AtCap_RefusesNewKeysButStillUpdatesExisting()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w, cap: 2);
		Assert.True(registry.Report(Creation("a", 1f, 1f)));
		Assert.True(registry.Report(Creation("b", 2f, 2f)));

		Assert.False(registry.Report(Creation("c", 3f, 3f)));
		Assert.Equal(2, registry.Count);

		Assert.True(registry.Report(Creation("a", 1f, 1f, keypadCode: "7777")), "an existing creation always updates");
		Assert.Contains(registry.Entries, e => e.Id == "a" && e.KeypadCode == "7777");
	}

	[Fact]
	public void Remove_DropsOnlyTheMatchingCreation()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		registry.Report(Creation("a", 1f, 1f));
		registry.Report(Creation("b", 2f, 2f));

		Assert.True(registry.Remove(Key("a", 1, 1)));
		Assert.False(registry.Remove(Key("a", 1, 1)), "a second remove is a no-op");
		Assert.Equal(1, registry.Count);
		Assert.Equal("b", Assert.Single(registry.Entries).Id);
	}

	[Fact]
	public void Remove_TwoCreationsInOneCell_DropsOnlyTheRequestedToken()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		registry.Report(Creation("turret", 5.2f, 7.2f, creator: 2001, sequence: 1));
		registry.Report(Creation("turret", 5.9f, 7.6f, creator: 2001, sequence: 2));

		Assert.True(registry.Remove(Key("turret", 5, 7, creator: 2001, sequence: 2)));

		Assert.Equal(1, registry.Count);
		Assert.Equal(1u, Assert.Single(registry.Entries).CreationSequence);
	}

	[Fact]
	public void ReportAnimal_KeepsTheKeyOnlyAndNeverEntersTheMaterializableEntries()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		var animal = Creation("crystalenemy", 4f, 5f, creator: 2001, sequence: 3);
		animal.IsAnimal = true;

		Assert.True(registry.ReportAnimal(animal));

		Assert.Equal(0, registry.Count);
		Assert.Empty(registry.Entries);
		Assert.Equal(1, registry.AnimalCount);
		Assert.Contains(Key("crystalenemy", 4, 5, creator: 2001, sequence: 3), registry.AnimalKeys);
	}

	[Fact]
	public void Remove_DropsTheAcceptedAnimalKeyToo()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		var animal = Creation("crystalenemy", 4f, 5f, creator: 2001, sequence: 3);
		animal.IsAnimal = true;
		registry.ReportAnimal(animal);

		Assert.True(registry.Remove(Key("crystalenemy", 4, 5, creator: 2001, sequence: 3)));

		Assert.Equal(0, registry.AnimalCount);
		Assert.Empty(registry.AnimalKeys);
	}

	[Fact]
	public void Remove_DropsTheKeyFromBothSets()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		var key = Key("crystalenemy", 4, 5, creator: 2001, sequence: 3);

		// A peer that flips IsAnimal between reports for one creation key lands
		// the key in both sets; the death drop must clear both, or the stale
		// animal acknowledgement rides every later snapshot.
		registry.Report(Creation("crystalenemy", 4f, 5f, creator: 2001, sequence: 3));
		var animal = Creation("crystalenemy", 4f, 5f, creator: 2001, sequence: 3);
		animal.IsAnimal = true;
		registry.ReportAnimal(animal);

		Assert.True(registry.Remove(key));

		Assert.Equal(0, registry.Count);
		Assert.Equal(0, registry.AnimalCount);
	}

	[Fact]
	public void ReportAnimal_AtCap_RefusesNewKeysBecauseTheBoundIsTotal()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w, cap: 2);
		var first = Creation("crystalenemy", 1f, 1f, creator: 2001, sequence: 1);
		first.IsAnimal = true;
		var second = Creation("crystalenemy", 2f, 2f, creator: 2001, sequence: 2);
		second.IsAnimal = true;

		// The cap bounds BOTH sets together (they ride one snapshot).
		Assert.True(registry.ReportAnimal(first));
		Assert.True(registry.Report(Creation("keypad", 3f, 3f)));
		Assert.False(registry.ReportAnimal(second), "a NEW animal key is refused at the total cap");
		Assert.True(registry.ReportAnimal(first), "an existing animal key always updates");

		Assert.Equal(1, registry.AnimalCount);
		Assert.Equal(1, registry.Count);
	}

	[Fact]
	public void GuestRole_IsANoOp()
	{
		using var w = ItemSimWorld.Create();
		var guestRegistry = w.G1.Services.GetRequiredService<RuntimeEntityRegistry>();

		Assert.False(guestRegistry.Report(Creation("keypad", 1f, 1f)));
		Assert.Equal(0, guestRegistry.Count);
	}

	[Fact]
	public void Reset_ClearsEveryAcceptedCreation()
	{
		using var w = ItemSimWorld.Create();
		var registry = Registry(w);
		registry.Report(Creation("a", 1f, 1f));
		registry.Report(Creation("b", 2f, 2f));
		var animal = Creation("crystalenemy", 3f, 3f, creator: 2001, sequence: 1);
		animal.IsAnimal = true;
		registry.ReportAnimal(animal);

		registry.Reset();

		Assert.Equal(0, registry.Count);
		Assert.Equal(0, registry.AnimalCount);
		Assert.Empty(registry.Entries);
		Assert.Empty(registry.AnimalKeys);
	}
}
