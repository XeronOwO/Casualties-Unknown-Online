using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The host-side user of the drop bound (<c>BlockDropBoundJudge</c>), driven over
/// the REAL runtime: two composed nodes, the real <see cref="IItemControl"/>, and
/// the adapter's judge built reflectively (the adapter assembly binds the game's
/// assemblies and is never compile-referenced here). What is pinned is the
/// composition the class exists for — a report's drops are bounded against the
/// destroyed block's own table, a refused drop is ANSWERED to the breaker over the
/// wire (the same <c>BlockAlreadyBroken</c> answer the first-writer loss uses,
/// which is what makes the breaker forget it and destroy its local copy), and the
/// caller is handed only the drops it may register and relay.
///
/// What is NOT verifiable here, and is named as such: the CALLER's half — that
/// <c>BlockBreakSync</c> registers and relays exactly the list this method returns,
/// and that it calls this BEFORE the relay so the breaker's pending set shrinks
/// first. That half needs the running game (the relay runs through Unity-typed
/// world state) and is read by the acceptance batch.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class BlockDropBoundJudgeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Bound_KeepsWhatTheDestroyedBlockCanProduce_AndAnswersTheRest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// Block 3's arm rolls scrapmetal at Random.Range(0.05f, 0.2f) and nothing
		// else: the rawcopper claim is the injection, the scrapmetal is a real roll.
		var accepted = Bound(judge, [Drop(11, "scrapmetal", 0.1f), Drop(12, "rawcopper")], destroyedBlock: 3);

		var kept = Assert.Single(accepted);
		Assert.Equal(11ul, kept.ItemId);
		Assert.Equal([12ul], rejects);
	}

	[Fact]
	public void Bound_ALegitimateSet_ComesBackWholeWithNoAnswer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// Block 11's arm is ONE roll over three ids: a report naming one of them is
		// ordinary play and must pass untouched.
		var accepted = Bound(judge, [Drop(31, "woodpanel")], destroyedBlock: 11);

		Assert.Equal([31ul], accepted.Select(drop => drop.ItemId));
		Assert.Empty(rejects);
	}

	[Fact]
	public void Bound_ABlockThatProducesNothing_AnswersEveryDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// Block 1 is on the game's no-loot path — nothing about it can drop anything.
		var accepted = Bound(judge, [Drop(41, "scrapmetal", 0.1f), Drop(42, "glassshards")], destroyedBlock: 1);

		Assert.Empty(accepted);
		Assert.Equal([41ul, 42ul], rejects);
	}

	// ---- the building-death half: the same report, the dying entities' own tables ----

	[Fact]
	public void BoundBuildingDrops_KeepsWhatTheDyingBuildingCanProduce_AndAnswersTheRest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// The buildings that lost support at this cell drop scrapmetal between 0.5
		// and 1: the scrapmetal is a real roll, the ilmenitechunk no row can produce.
		var accepted = BoundBuilding(judge, [Row("scrapmetal", 0.5f, 1f)], [TrapDrop(11, "scrapmetal", 0.7f), TrapDrop(12, "ilmenitechunk")]);

		var kept = Assert.Single(accepted);
		Assert.Equal(11ul, kept.ItemId);
		Assert.Equal([12ul], rejects);
	}

	[Fact]
	public void BoundBuildingDrops_ALegitimateSet_ComesBackWholeWithNoAnswer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// The window's own edge is a legitimate roll (Random.Range is inclusive), so
		// an ordinary support-loss death must pass untouched.
		var accepted = BoundBuilding(judge, [Row("scrapmetal", 0.5f, 1f)], [TrapDrop(21, "scrapmetal", 0.5f)]);

		Assert.Equal([21ul], accepted.Select(drop => drop.ItemId));
		Assert.Empty(rejects);
	}

	[Fact]
	public void BoundBuildingDrops_NoBuildingLostSupportHere_AnswersEveryDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// This host marked no building as losing support at the cell, so no roll for
		// one can have produced these: the safe direction is to refuse them, because
		// a report must not introduce a building's drops where no building died.
		var accepted = BoundBuilding(judge, [], [TrapDrop(31, "scrapmetal", 0.7f), TrapDrop(32, "woodpanel")]);

		Assert.Empty(accepted);
		Assert.Equal([31ul, 32ul], rejects);
	}

	[Fact]
	public void BoundBuildingDrops_OneRowAccountsForOneDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// One guaranteed roll is one row: a single row over three pool ids still
		// produces ONE item, so a report claiming two of them loses the second.
		var accepted = BoundBuilding(judge, [Row("woodscraps", "stick", "woodpanel")], [TrapDrop(41, "stick"), TrapDrop(42, "woodpanel")]);

		Assert.Single(accepted);
		Assert.Single(rejects);
	}

	// ---- the adapter type (reflection: the test project never compile-references the adapter assembly) ----

	private static object CreateJudge(IItemControl items)
	{
		var factory = LoggerFactory.Create(_ => { });
		var tileContentType = AdapterType("CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterTileContentProvider");
		var tileContent = Activator.CreateInstance(tileContentType, CreateLogger(factory, tileContentType))!;
		var judgeType = AdapterType("CasualtiesUnknownOnline.GameAdapter.World.BlockDropBoundJudge");
		return Activator.CreateInstance(judgeType, items, tileContent, CreateLogger(factory, judgeType))!;
	}

	private static Type AdapterType(string name) =>
		GameAssemblyHost.Adapter.GetType(name) ?? throw new InvalidOperationException($"{name} not found in the GameAdapter assembly.");

	private static object CreateLogger(ILoggerFactory factory, Type type) =>
		typeof(LoggerFactoryExtensions)
			.GetMethods()
			.Single(method => method.Name == "CreateLogger" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
			.MakeGenericMethod(type)
			.Invoke(null, [factory])!;

	private static IReadOnlyList<BlockDropEntryMsg> Bound(object judge, List<BlockDropEntryMsg> drops, ushort destroyedBlock)
	{
		var cell = Activator.CreateInstance(
			GameAssemblyHost.ResolveType("UnityEngine.Vector2Int") ?? throw new InvalidOperationException("UnityEngine.Vector2Int not found."),
			6, 6)!;
		var method = judge.GetType().GetMethod("Bound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BlockDropBoundJudge.Bound not found.");

		return (IReadOnlyList<BlockDropEntryMsg>)method.Invoke(judge, [GuestId, cell, destroyedBlock, drops])!;
	}

	private static BlockDropEntryMsg Drop(ulong instanceId, string itemId, float condition = 1f) =>
		new() { ItemId = instanceId, Item = new CharacterItemMsg { ItemId = itemId, Condition = condition } };

	private static TrapDropEntryMsg TrapDrop(ulong instanceId, string itemId, float condition = 1f) =>
		new() { ItemId = instanceId, Item = new CharacterItemMsg { ItemId = itemId, Condition = condition } };

	private static DropBound Row(params string[] itemIds) => new(itemIds);

	private static DropBound Row(string itemId, float min, float max) => new([itemId], min, max);

	private static IReadOnlyList<TrapDropEntryMsg> BoundBuilding(object judge, IReadOnlyList<DropBound> bounds, List<TrapDropEntryMsg> drops)
	{
		var cell = Activator.CreateInstance(
			GameAssemblyHost.ResolveType("UnityEngine.Vector2Int") ?? throw new InvalidOperationException("UnityEngine.Vector2Int not found."),
			6, 6)!;
		var method = judge.GetType().GetMethod("BoundBuildingDrops", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BlockDropBoundJudge.BoundBuildingDrops not found.");

		return (IReadOnlyList<TrapDropEntryMsg>)method.Invoke(judge, [GuestId, cell, bounds, drops])!;
	}
}
