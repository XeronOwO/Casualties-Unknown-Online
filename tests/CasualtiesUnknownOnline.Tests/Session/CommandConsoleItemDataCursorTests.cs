using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>cursor</c> target through the real console: the item the pointer names is the
/// one a read reads and a write writes, and every way the pointer cannot name one keeps
/// its own answer. The scene's answer is staged here — the pointer query itself is the
/// Game Adapter's, whose shape is pinned by the native call site rather than by a unit
/// test — so what this class proves is the wiring: which id the target resolves to, that
/// the record (not the local capture) answers a read, that a write takes the route the
/// row's location chooses, and that a refused pointer writes nothing.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleItemDataCursorTests
{
	private const ulong Backpack = 4101;

	/// <summary>
	/// A world row is the shape the pointer actually names: a carried item's world object
	/// stops simulating the moment it is picked up, so the pointer query can only return
	/// world items.
	/// </summary>
	[Fact]
	public void Get_Cursor_ReadsTheRecordedRowNotTheLocalCapture()
	{
		var (host, _, local) = CreateSceneSession();
		SeedWorld(host, HostId, Item(Backpack, "backpack", condition: 0.8f));
		local.Hold(Item(Backpack, "backpack", condition: 0.4f));
		PointAt(local, LocalCursorItemKind.Resolved, Backpack);

		var answer = Run(host, "/data get cursor");

		Assert.Contains("#4101 backpack", answer);
		Assert.Contains("Condition:0.8f", answer);
		Assert.DoesNotContain("Condition:0.4f", answer);
	}

	[Fact]
	public void Get_Cursor_OnAnUnrecordedId_IsRefusedByName()
	{
		var (host, _, local) = CreateSceneSession();
		PointAt(local, LocalCursorItemKind.Resolved, Backpack);

		var answer = Run(host, "/data get cursor");

		Assert.Contains("No item #4101 is recorded in this session.", answer);
	}

	[Fact]
	public void Get_Cursor_OnNothingUnderThePointer_IsRefusedByName()
	{
		var (host, _, local) = CreateSceneSession();
		PointAt(local, LocalCursorItemKind.None);

		var answer = Run(host, "/data get cursor");

		Assert.Contains("Nothing is under the pointer", answer);
	}

	[Fact]
	public void Get_Cursor_OnAnIdlessItem_IsRefusedByName()
	{
		var (host, _, local) = CreateSceneSession();
		PointAt(local, LocalCursorItemKind.NoInstanceId);

		var answer = Run(host, "/data get cursor");

		Assert.Contains("has no CUO instance id on this client yet", answer);
		Assert.Contains("item snapshot", answer);
	}

	/// <summary>
	/// The write goes the same way the <c>id</c> target does — the item's own local
	/// capture, one commit, then the report the ROW's location chooses — so the cursor
	/// only changes HOW the id was found. A world row takes the correction route.
	/// </summary>
	[Fact]
	public void Set_Cursor_WritesAWorldItemThroughTheCorrectionRoute()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", condition: 0.5f);
		SeedWorld(host, HostId, item);
		local.Hold(item);
		PointAt(local, LocalCursorItemKind.Resolved, Backpack);
		var reports = RecordReports(host);

		var answer = Run(host, "/data modify cursor Condition set 0.25f");

		Assert.Contains("#4101 backpack: Condition 0.5f -> 0.25f", answer);
		Assert.Equal(0.25f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Contains(reports.Corrected, corrected => corrected.InstanceId == Backpack && corrected.Condition == 0.25f);
		Assert.Empty(reports.Carried);
	}

	[Fact]
	public void Set_Cursor_OnNothingUnderThePointer_IsRefusedWithoutWriting()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", condition: 0.5f);
		SeedWorld(host, HostId, item);
		local.Hold(item);
		PointAt(local, LocalCursorItemKind.None);

		var answer = Run(host, "/data modify cursor Condition set 0.25f");

		Assert.Contains("Nothing is under the pointer", answer);
		Assert.Empty(local.Written);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
	}

	/// <summary>
	/// The pointer names a local object the record has no row for: the write refuses
	/// rather than applying it, because the report's route is chosen FROM the row.
	/// </summary>
	[Fact]
	public void Set_Cursor_OnAnIdTheRecordDoesNotName_IsRefusedByName()
	{
		var (host, _, local) = CreateSceneSession();
		local.Hold(Item(Backpack, "backpack", condition: 0.5f));
		PointAt(local, LocalCursorItemKind.Resolved, Backpack);

		var answer = Run(host, "/data modify cursor Condition set 0.25f");

		Assert.Contains("#4101 is under the pointer but not recorded in this session yet", answer);
		Assert.Empty(local.Written);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
	}

	[Fact]
	public void Merge_Cursor_ChangesEveryPropertyItNames()
	{
		var (host, _, local) = CreateSceneSession();
		var item = Item(Backpack, "backpack", condition: 0.5f);
		SeedWorld(host, HostId, item);
		local.Hold(item);
		PointAt(local, LocalCursorItemKind.Resolved, Backpack);

		var answer = Run(host, "/data merge cursor {Condition:0.6f,Favourited:true}");

		Assert.Contains("Condition 0.5f -> 0.6f", answer);
		Assert.Contains("Favourited false -> true", answer);
		Assert.Equal(0.6f, local.Held(Backpack)!.Condition);
		Assert.True(local.Held(Backpack)!.Favourited);
	}

	/// <summary>Every console test runs against a scene of its own, so nothing a test writes leaks into another.</summary>
	private static (TestNode Host, TestNode Guest, FakeLocalItemAccess Local) CreateSceneSession()
	{
		var local = new FakeLocalItemAccess();
		var (host, guest) = CreateSession(services => services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local)));
		return (host, guest, local);
	}

	/// <summary>What the pointer answers, for a test that stages the scene's own classification.</summary>
	private static void PointAt(FakeLocalItemAccess local, LocalCursorItemKind kind, ulong instanceId = 0)
	{
		local.Cursor = kind;
		local.CursorInstanceId = instanceId;
	}

	/// <summary>Puts one item into the record as a WORLD row — the row a dropped or ground item has, and the shape the pointer can return.</summary>
	private static void SeedWorld(TestNode node, ulong owner, CharacterItemMsg item) =>
		Assert.True(Kernel(node).TrySpawn(owner, new ItemIdentity(item.InstanceId, item.ItemId), ItemLocation.World(3f, 4f), item, out _, out _));

	private static CharacterItemMsg Item(ulong instanceId, string itemId, float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = 0,
		Condition = condition,
	};

	/// <summary>
	/// The two report routes are told apart by what they RAISE, not by the row changing:
	/// the carried-fact route publishes the owner's own fact, the world route corrects
	/// every other copy.
	/// </summary>
	private static (List<CharacterItemMsg> Carried, List<CharacterItemMsg> Corrected) RecordReports(TestNode host)
	{
		var carried = new List<CharacterItemMsg>();
		var corrected = new List<CharacterItemMsg>();
		var items = host.Services.GetRequiredService<IItemControl>();
		items.ItemCarriedSyncReceived += (_, item, _) => carried.Add(item);
		items.ItemCorrectionReceived += corrected.Add;
		return (carried, corrected);
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
