using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The host-side user of the entity-event drop bound (<c>TrapDropBoundJudge</c>),
/// driven over the REAL runtime: two composed nodes, the real
/// <see cref="IItemControl"/>, and the adapter's judge built reflectively (the
/// adapter assembly binds the game's assemblies and is never compile-referenced
/// here). What is pinned is the composition the class exists for — a destructive
/// event's reported drops are bounded against the tables of the entities this
/// host's own application destroyed or damaged, a refused drop is ANSWERED to the
/// reporter over the wire with its own reason (<c>TrapDropUnaccounted</c>, which
/// is what makes the reporter destroy its local copy), and the caller is handed
/// only the drops it may register and relay.
///
/// What is NOT verifiable here, and is named as such: the CAPTURE and the
/// CALLER's half — that <c>TrapBuildingHealthScope</c> records a row per entity
/// this host's blast damaged, that <c>EntityEventSync</c> adds the trap entity's
/// own table and feeds the accepted list to the kernel batch AND the relay. Those
/// run through Unity-typed scene state (live components, an explosion diff) and
/// are read by the acceptance batch.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class TrapDropBoundJudgeTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Bound_KeepsWhatTheApplicationsTablesCanProduce_AndAnswersTheRest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<(ulong ItemId, ItemRejectMsg.Reason Reason)>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, reason) => rejects.Add((itemId, reason));
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// The entities this host's own application touched drop scrapmetal between
		// 0.5 and 1: the scrapmetal is a real roll, the ilmenitechunk no row can
		// produce, so the second is the creation claim this bound exists to stop.
		var accepted = Bound(judge, EntityEventKind.MineExploded, [Row("scrapmetal", 0.5f, 1f)], [TrapDrop(11, "scrapmetal", 0.7f), TrapDrop(12, "ilmenitechunk")]);

		var kept = Assert.Single(accepted);
		Assert.Equal(11ul, kept.ItemId);
		var rejected = Assert.Single(rejects);
		Assert.Equal(12ul, rejected.ItemId);
		Assert.Equal(ItemRejectMsg.Reason.TrapDropUnaccounted, rejected.Reason);
	}

	[Fact]
	public void Bound_ALegitimateSet_ComesBackWholeWithNoAnswer()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// The window's own edge is a legitimate roll (Random.Range is inclusive),
		// so an ordinary destructive-event report must pass untouched.
		var accepted = Bound(judge, EntityEventKind.CrystalUnstableExploded, [Row("scrapmetal", 0.5f, 1f)], [TrapDrop(21, "scrapmetal", 0.5f)]);

		Assert.Equal([21ul], accepted.Select(drop => drop.ItemId));
		Assert.Empty(rejects);
	}

	[Fact]
	public void Bound_NoTableAccountsForTheSet_AnswersEveryDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// This host's own application destroyed or damaged no entity with a drop
		// table, so no roll for one can have produced these: the safe direction is
		// to refuse them, because a report must not introduce a dead entity's drops
		// where this host accounted for none.
		var accepted = Bound(judge, EntityEventKind.MineExploded, [], [TrapDrop(31, "scrapmetal", 0.7f), TrapDrop(32, "woodpanel")]);

		Assert.Empty(accepted);
		Assert.Equal([31ul, 32ul], rejects);
	}

	[Fact]
	public void Bound_AKindOutsideTheBuildingDeathCarrier_AnswersEveryDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// A door opening is outside the building-death carrier's profile, so its event
		// has no dying entity behind a drop — even a table this host could read must not
		// turn the event into an item-creation channel.
		var accepted = Bound(judge, EntityEventKind.ShuttleDoorOpened, [Row("scrapmetal", 0.5f, 1f)], [TrapDrop(41, "scrapmetal", 0.7f)]);

		Assert.Empty(accepted);
		Assert.Equal([41ul], rejects);
	}

	[Fact]
	public void Bound_OneRowAccountsForOneDrop()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var rejects = new List<ulong>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, _) => rejects.Add(itemId);
		var judge = CreateJudge(host.Services.GetRequiredService<IItemControl>());

		// One guaranteed roll is one row: a single row over three pool ids still
		// produces ONE item, so a report claiming two of them loses the second.
		var accepted = Bound(judge, EntityEventKind.TurretSelfDestructed, [Row("woodscraps", "stick", "woodpanel")], [TrapDrop(51, "stick"), TrapDrop(52, "woodpanel")]);

		Assert.Single(accepted);
		Assert.Single(rejects);
	}

	// ---- the adapter type (reflection: the test project never compile-references the adapter assembly) ----

	private static object CreateJudge(IItemControl items)
	{
		var factory = LoggerFactory.Create(_ => { });
		var judgeType = AdapterType("CasualtiesUnknownOnline.GameAdapter.World.TrapDropBoundJudge");
		return Activator.CreateInstance(judgeType, items, CreateLogger(factory, judgeType))!;
	}

	private static Type AdapterType(string name) =>
		GameAssemblyHost.Adapter.GetType(name) ?? throw new InvalidOperationException($"{name} not found in the GameAdapter assembly.");

	private static object CreateLogger(ILoggerFactory factory, Type type) =>
		typeof(LoggerFactoryExtensions)
			.GetMethods()
			.Single(method => method.Name == "CreateLogger" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
			.MakeGenericMethod(type)
			.Invoke(null, [factory])!;

	private static IReadOnlyList<TrapDropEntryMsg> Bound(object judge, EntityEventKind kind, IReadOnlyList<DropBound> bounds, List<TrapDropEntryMsg> drops)
	{
		var position = Activator.CreateInstance(
			GameAssemblyHost.ResolveType("UnityEngine.Vector2") ?? throw new InvalidOperationException("UnityEngine.Vector2 not found."),
			10f, 20f)!;
		var method = judge.GetType().GetMethod("Bound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TrapDropBoundJudge.Bound not found.");

		return (IReadOnlyList<TrapDropEntryMsg>)method.Invoke(judge, [GuestId, kind, position, bounds, drops])!;
	}

	private static TrapDropEntryMsg TrapDrop(ulong instanceId, string itemId, float condition = 1f) =>
		new() { ItemId = instanceId, Item = new CharacterItemMsg { ItemId = itemId, Condition = condition } };

	private static DropBound Row(params string[] itemIds) => new(itemIds);

	private static DropBound Row(string itemId, float min, float max) => new([itemId], min, max);
}
