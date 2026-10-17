using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Stage D's first question, answered against the real channel, registry, kernel and world-entry fan-out: what does
/// a member that joins AFTER a summoned object see?
///
/// <para>
/// The console verb is the real one; the scene seam is <see cref="FakeWorldSummon"/>, which "summons" the object
/// and makes exactly the report the Game Adapter's own Start hooks make. Everything downstream is production:
/// the item row (the same one the give ladder's drop writes), the accepted-creation table, the enemy-domain
/// acknowledgement and the world-entry snapshot group. Each case makes the creation while the later joiner's link
/// is down (the lazy-session swallow the recovery exists for), then brings that member into the world and asserts
/// the family's absolute table carries it — the claim this ticket required before the verb could land.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class WorldSummonLateJoinerTests
{
	[Fact]
	public void AnEntitySummon_ReachesALaterJoinerThroughTheAcceptedCreationTable()
	{
		using var w = CreateWorld();
		var creations = new List<EntitySpawnedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => creations.Add(msg);

		var answer = CreateWhileTheJoinerIsAway(w);

		Assert.Equal("Summoned beartrap at (1.0, 2.0).", answer);
		Assert.Equal(1, Registry(w).Count);

		// The later joiner: it was not in the world (and could hear nothing) when the object was created.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var creation = Assert.Single(creations);
		Assert.Equal("beartrap", creation.Id);
		Assert.Equal(1f, creation.Position.X);
		Assert.Equal(2f, creation.Position.Y);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntitySnapshot));
	}

	[Fact]
	public void AnItemEntitySummon_ReachesALaterJoinerThroughTheItemSnapshotAndTheKernelRow()
	{
		using var w = CreateWorld();

		CreateWhileTheJoinerIsAway(w, "/summon item bandage player");
		var item = Assert.Single(Summon(w).CreatedItems);

		// The later joiner: the world-entry group carries the item snapshot and the kernel checkpoint.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Equal(1, w.SpawnedEvents(w.G1));
		Assert.NotNull(w.G1.Services.GetRequiredService<ItemKernelAuthority>().FindItem(item.InstanceId));
	}

	/// <summary>
	/// An animal is acknowledged by KEY on the creation channel (the enemy domain owns its late-join copy), so the
	/// snapshot a later joiner receives carries the key and no materializable entry — the fact the enemy domain's
	/// own runtime-spawn backfill is fed from.
	/// </summary>
	[Fact]
	public void AnAnimalSummon_IsAcknowledgedByKeyInTheSnapshotALaterJoinerReceives()
	{
		using var w = CreateWorld();
		RuntimeEntitySnapshotMsg? snapshot = null;
		w.G1.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.RuntimeEntitySnapshot)
			{
				snapshot = NetPacket.DecodePayload<RuntimeEntitySnapshotMsg>(frame);
			}
		};

		CreateWhileTheJoinerIsAway(w, "/summon spider player");

		Assert.Equal(1, Registry(w).AnimalCount);
		Assert.Equal(0, Registry(w).Count); // never a materializable creation

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.NotNull(snapshot);
		Assert.Empty(snapshot!.Entries);
		var key = Assert.Single(snapshot.AcceptedAnimalKeys);
		Assert.Equal("spider", key.Id);
	}

	/// <summary>One host <c>/summon</c> while the later joiner's link is down, so only the world-entry group can carry the result there.</summary>
	private static string CreateWhileTheJoinerIsAway(ItemSimWorld world, string line = "/summon beartrap player")
	{
		world.Driver.Network.SetFaults(world.Host.SteamId, world.G1.SteamId, new LinkFaults { Down = true });
		var answer = Run(world.Host, line);
		world.Driver.Tick(33);
		world.Driver.Network.ClearFaults(world.Host.SteamId, world.G1.SteamId);
		return answer;
	}

	private static ItemSimWorld CreateWorld() =>
		ItemSimWorld.Create(services => services.Replace(ServiceDescriptor.Singleton<IWorldSummon>(p => new FakeWorldSummon(
			p.GetRequiredService<IItemControl>(),
			p.GetRequiredService<IWorldControl>(),
			p.GetRequiredService<ISessionControl>()))));

	private static FakeWorldSummon Summon(ItemSimWorld world) =>
		(FakeWorldSummon)world.Host.Services.GetRequiredService<IWorldSummon>();

	private static RuntimeEntityRegistry Registry(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<RuntimeEntityRegistry>();

	/// <summary>Runs one console line on the host and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
