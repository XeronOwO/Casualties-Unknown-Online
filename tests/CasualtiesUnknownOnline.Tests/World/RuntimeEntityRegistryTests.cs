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
	private static EntitySpawnedMsg Creation(string id, float x, float y, string keypadCode = "") => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		KeypadCode = keypadCode,
	};

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

		Assert.True(registry.Remove("a", 1f, 1f));
		Assert.False(registry.Remove("a", 1f, 1f), "a second remove is a no-op");
		Assert.Equal(1, registry.Count);
		Assert.Equal("b", Assert.Single(registry.Entries).Id);
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

		registry.Reset();

		Assert.Equal(0, registry.Count);
		Assert.Empty(registry.Entries);
	}
}
