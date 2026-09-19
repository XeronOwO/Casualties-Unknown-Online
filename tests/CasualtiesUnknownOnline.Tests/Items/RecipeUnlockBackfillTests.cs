using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// Recipe-unlock backfill (sync-coverage audit I6). The unlock is a per-process
/// static write (<c>Recipes.recipes[idx].INT = 0</c>) and its report/relay is
/// one-shot, so a swallowed send or a member that joined later used to leave a
/// crafting list short with nothing to heal it. The absolute SET is the fact:
/// the host sends its live table's unlocked indices on the world-entry and 60 s
/// repair groups, a guest reports its own on the shared fallback cadence until
/// the host's set carries it, and the host merges a guest's set through the
/// ordinary unlock path (apply + relay), so the host stays the authority.
/// <para>
/// The live recipe table is faked at the <see cref="INativeWorldFacts"/> seam —
/// one fake per NODE, because the real table is a per-process static and the
/// host's set and a guest's set are therefore different tables — and the
/// adapter's own write is mirrored onto it (<see cref="BindNativeTableWrites"/>),
/// because the port reads the very table that write touches. What this suite does
/// NOT execute is the rest of the adapter's half: the <c>INT == 0</c> read of the
/// real <c>Recipes.recipes</c>, the silent set write and the crafting-list
/// refresh of <c>RecipeUnlockApply</c> (all game types), the real host's 60 s
/// cycle (<c>WorldEventSync.Update</c>) that calls the repair group, and the
/// protobuf round trip of the new message. Those rest on code review plus the
/// unified dual-client acceptance pass, and the ticket says so.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class RecipeUnlockBackfillTests
{
	/// <summary>
	/// One native recipe table per node. The adapter's table is a per-process
	/// static, so a single shared fake would make "the host's set" and "the
	/// guest's set" the same object and every assertion below would prove nothing.
	/// </summary>
	private sealed class NodeRecipeTables
	{
		private readonly Dictionary<ulong, FakeNativeWorldFacts> _byNode = [];

		internal INativeWorldFacts For(IServiceProvider provider) =>
			Table(provider.GetRequiredService<ISessionControl>().LocalSteamId);

		internal FakeNativeWorldFacts Table(ulong steamId)
		{
			if (!_byNode.TryGetValue(steamId, out var table))
			{
				table = new FakeNativeWorldFacts();
				_byNode[steamId] = table;
			}

			return table;
		}
	}

	private static ItemSimWorld Create(NodeRecipeTables tables)
	{
		var w = ItemSimWorld.Create(services => services.AddSingleton(tables.For));
		BindNativeTableWrites(w, tables);
		return w;
	}

	/// <summary>
	/// The adapter's write, in the sim's terms: <c>RecipeUnlockApply</c> sets
	/// <c>Recipes.recipes[idx].INT = 0</c> when the Runtime raises an unlock, and
	/// the port reads that same table — so a side that applies an unlock must see
	/// it in its own fake. Without this the host's merge would re-learn an index
	/// it had just applied, and a guest could never prove the host's set carries
	/// its unlock.
	/// </summary>
	private static void BindNativeTableWrites(ItemSimWorld w, NodeRecipeTables tables)
	{
		foreach (var node in new[] { w.Host, w.G1, w.G2 })
		{
			var table = tables.Table(node.SteamId);
			var craft = node.Services.GetRequiredService<ICraftControl>();
			craft.RecipeUnlockReceived += table.SeedUnlockedRecipe;
			craft.RecipeUnlockSetReceived += set =>
			{
				foreach (var index in set)
				{
					table.SeedUnlockedRecipe(index);
				}
			};
		}
	}

	/// <summary>
	/// A guest's blueprint use through the REAL path. <c>ItemSimWorld.Unlock</c>
	/// puts the raw frame on the wire and never reaches
	/// <c>CraftSyncService.SendRecipeUnlock</c>, which is what arms the fallback.
	/// </summary>
	private static void GuestUnlocks(TestNode guest, int recipeIndex) =>
		guest.Services.GetRequiredService<ICraftControl>().SendRecipeUnlock(recipeIndex);

	/// <summary>A live count of one message id the HOST received — the sim world records the guests' frames only.</summary>
	private static Func<int> HostReceives(ItemSimWorld w, NetMsg msg)
	{
		var count = 0;
		w.Host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == msg)
			{
				count++;
			}
		};
		return () => count;
	}

	/// <summary>Put the entering member in the world so the entry group fires.</summary>
	private static void EnterWorld(ItemSimWorld w) =>
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	[Fact]
	public void UnconfirmedUnlock_StopsAfterTheBudget_InsteadOfReReportingForever()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.G1.SteamId).SeedUnlockedRecipe(9);
		var hostSets = HostReceives(w, NetMsg.RecipeUnlockSnapshot);

		// The unlock is armed and its live report swallowed, and this host never
		// sends a set back (the test never calls the repair group), so the index can
		// never be confirmed: the budget is the only thing that ends the loop.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		GuestUnlocks(w.G1, 9);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		for (var window = 0; window < 4; window++)
		{
			w.Driver.Tick(61_000);
		}

		// Three windows re-report (the per-index budget); the fourth finds it spent,
		// drops the index by name and sends nothing — rather than dripping one
		// message a minute for the rest of the session.
		Assert.Equal(3, hostSets());
	}

	[Fact]
	public void ANewUnlock_AfterTheBudgetIsSpent_ReArmsTheFallback()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.G1.SteamId).SeedUnlockedRecipe(9);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(9); // held here, so the later set has nothing to relay
		var hostSets = HostReceives(w, NetMsg.RecipeUnlockSnapshot);

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		GuestUnlocks(w.G1, 9);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		for (var window = 0; window < 4; window++)
		{
			w.Driver.Tick(61_000);
		}

		Assert.Equal(3, hostSets());

		// A later unlock is a new fact with its own budget: it arms a fresh window.
		tables.Table(w.G1.SteamId).SeedUnlockedRecipe(12);
		GuestUnlocks(w.G1, 12);
		w.Driver.Tick(33);
		w.Driver.Tick(61_000);

		Assert.Equal(4, hostSets());
	}

	[Fact]
	public void LateJoiner_ReceivesTheHostsUnlockedSetOnEntry_WithoutAlerts()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(3);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(7);

		var applied = new List<IReadOnlyList<int>>();
		var alerts = 0;
		var guestCraft = w.G1.Services.GetRequiredService<ICraftControl>();
		guestCraft.RecipeUnlockSetReceived += set => applied.Add(set);
		guestCraft.RecipeUnlockReceived += _ => alerts++;

		EnterWorld(w);
		w.Driver.Tick(33);

		Assert.Equal<int>([3, 7], Assert.Single(applied));
		Assert.Equal(0, alerts); // a backfill is not a learn: no per-recipe alert
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RecipeUnlockSnapshot));
	}

	[Fact]
	public void EmptyUnlockSet_IsNotSent()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);

		var applied = new List<IReadOnlyList<int>>();
		w.G1.Services.GetRequiredService<ICraftControl>().RecipeUnlockSetReceived += set => applied.Add(set);

		EnterWorld(w);
		w.Driver.Tick(33);

		Assert.Empty(applied);
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.RecipeUnlockSnapshot));
	}

	[Fact]
	public void UnlockMadeAfterTheEntry_RidesTheSixtySecondRepair()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		var applied = new List<IReadOnlyList<int>>();
		w.G1.Services.GetRequiredService<ICraftControl>().RecipeUnlockSetReceived += set => applied.Add(set);

		EnterWorld(w);
		w.Driver.Tick(33);
		Assert.Empty(applied);

		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(11);
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(33);

		Assert.Equal<int>([11], Assert.Single(applied));
	}

	[Fact]
	public void HostWithoutALiveRecipeTable_SendsNoSnapshot()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(3);
		tables.Table(w.Host.SteamId).CaptureFailure = "no live world is present";

		EnterWorld(w);
		w.Driver.Tick(33);

		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.RecipeUnlockSnapshot));
	}

	[Fact]
	public void SwallowedGuestUnlock_ConvergesThroughTheGuestsSetReReport()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		var hostLearned = new List<int>();
		w.Host.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += index => hostLearned.Add(index);

		// The guest's own table holds the unlock (the native use wrote INT = 0)
		// and the live report is swallowed by the lazy link.
		tables.Table(w.G1.SteamId).SeedUnlockedRecipe(9);
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		GuestUnlocks(w.G1, 9);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		Assert.Empty(hostLearned);

		// Inside the window the fallback is silent (the live report just went
		// out); past it the guest reports the ABSOLUTE set, the host merges the
		// difference and relays it to the other members.
		w.Driver.Tick(59_000);
		Assert.Empty(hostLearned);
		w.Driver.Tick(2_000);

		Assert.Equal<int>([9], hostLearned);
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));
	}

	[Fact]
	public void HostsSet_EndsTheGuestsReReport()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.G1.SteamId).SeedUnlockedRecipe(4);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(4); // this host already holds it
		var hostSets = HostReceives(w, NetMsg.RecipeUnlockSnapshot);

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		GuestUnlocks(w.G1, 4);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		w.Driver.Tick(61_000); // the fallback's first report
		Assert.Equal(1, hostSets());

		// The host's own set (the repair group) carries the index — that is the
		// answer, so the window that follows reports nothing.
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(33);
		w.Driver.Tick(61_000);

		Assert.Equal(1, hostSets());
	}

	[Fact]
	public void HostMerge_RelaysOnlyTheIndicesItHadNotLearned()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.Host.SteamId).SeedUnlockedRecipe(2);
		var hostLearned = new List<int>();
		w.Host.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += index => hostLearned.Add(index);

		// A guest reports its whole set: 2 is one this host already holds, 5 is new.
		var hostCraft = w.Host.Services.GetRequiredService<ICraftControl>();
		hostCraft.FireRecipeUnlockSnapshotReceived(w.G1.SteamId, [2, 5]);
		w.Driver.Tick(33);

		Assert.Equal<int>([5], hostLearned);
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));

		// The same set again costs one comparison and nothing else.
		hostCraft.FireRecipeUnlockSnapshotReceived(w.G1.SteamId, [2, 5]);
		w.Driver.Tick(33);

		Assert.Equal<int>([5], hostLearned);
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));
	}

	[Fact]
	public void HostWithoutALiveTable_DoesNotJudgeAGuestsSet()
	{
		var tables = new NodeRecipeTables();
		using var w = Create(tables);
		tables.Table(w.Host.SteamId).CaptureFailure = "no live world is present";
		var hostLearned = new List<int>();
		w.Host.Services.GetRequiredService<ICraftControl>().RecipeUnlockReceived += index => hostLearned.Add(index);

		w.Host.Services.GetRequiredService<ICraftControl>().FireRecipeUnlockSnapshotReceived(w.G1.SteamId, [5]);
		w.Driver.Tick(33);

		// No table to compare against means no judgment: nothing is applied and
		// nothing is relayed (the reporter's set comes back on the next cycle).
		Assert.Empty(hostLearned);
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.RecipeUnlock));
	}
}
