using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The CloneFactTable's nested carried-sync apply, exercised reflectively (the
/// adapter is compile-excluded from the test project): a container-content
/// event can target a top-level container OR a nested container inside it, and
/// the fact table must replace the exact node in the recursive contents — never
/// append the nested container as a phantom top-level item.
/// </summary>
public class CloneFactTableNestedCarriedSyncTests
{
	private static readonly Type CloneFactTable = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneFactTable",
		throwOnError: true)!;

	[Fact]
	public void ApplyCarriedSync_ReplacesNestedContainerInsideContents()
	{
		var table = CreateTable();
		const ulong owner = 2001;
		var backpack = new CharacterItemMsg
		{
			InstanceId = 101,
			ItemId = "backpack",
			Contents =
			[
				new CharacterItemMsg
				{
					InstanceId = 202,
					ItemId = "pouch",
					Contents = [new CharacterItemMsg { InstanceId = 303, ItemId = "knife" }],
				},
			],
		};
		Invoke(table, "ApplySnapshot", owner, new CharacterDataMsg { OwnerSteamId = owner, Items = [backpack] });

		var updatedBackpack = new CharacterItemMsg
		{
			InstanceId = 101,
			ItemId = "backpack",
			Contents =
			[
				new CharacterItemMsg
				{
					InstanceId = 202,
					ItemId = "pouch",
					Contents = [new CharacterItemMsg { InstanceId = 304, ItemId = "bandage" }],
				},
			],
		};
		Invoke(table, "ApplyCarriedSync", owner, updatedBackpack, true);

		var cloneData = CloneData(table);
		var stored = Assert.Single(cloneData[owner].Items);
		var pouch = Assert.Single(stored.Contents);
		Assert.Equal(202ul, pouch.InstanceId);
		Assert.Equal(304ul, Assert.Single(pouch.Contents).InstanceId);
	}

	[Fact]
	public void ApplyCarriedSync_WhenParentGainsMovedChild_RemovesOldTopLevelCopy()
	{
		var table = CreateTable();
		const ulong owner = 2001;
		var trashbag = new CharacterItemMsg { InstanceId = 101, ItemId = "trashbag" };
		var waterbottle = new CharacterItemMsg { InstanceId = 202, ItemId = "waterbottle" };
		Invoke(table, "ApplySnapshot", owner, new CharacterDataMsg
		{
			OwnerSteamId = owner,
			Items = [trashbag, waterbottle],
		});

		var updatedTrashbag = new CharacterItemMsg
		{
			InstanceId = 101,
			ItemId = "trashbag",
			Contents = [waterbottle],
		};
		Invoke(table, "ApplyCarriedSync", owner, updatedTrashbag, true);

		var cloneData = CloneData(table);
		Assert.True(cloneData[owner].Items.Count == 1,
			$"expected one top-level item after prune, got {cloneData[owner].Items.Count}: "
			+ string.Join(", ", cloneData[owner].Items.Select(i => $"{i.ItemId}#{i.InstanceId}")));
		var stored = cloneData[owner].Items[0];
		Assert.Equal(101ul, stored.InstanceId);
		var moved = Assert.Single(stored.Contents);
		Assert.Equal(202ul, moved.InstanceId);
		Assert.Equal("waterbottle", moved.ItemId);
	}

	[Fact]
	public void ApplyCarriedSync_WhenItemMovesOutOfContainer_MovesItToTopLevel()
	{
		var table = CreateTable();
		const ulong owner = 2001;
		var waterbottle = new CharacterItemMsg { InstanceId = 202, ItemId = "waterbottle" };
		var trashbag = new CharacterItemMsg
		{
			InstanceId = 101,
			ItemId = "trashbag",
			Contents = [waterbottle],
		};
		Invoke(table, "ApplySnapshot", owner, new CharacterDataMsg
		{
			OwnerSteamId = owner,
			Items = [trashbag],
		});

		var movedOut = new CharacterItemMsg
		{
			InstanceId = 202,
			ItemId = "waterbottle",
			SlotIndex = 0,
		};
		Invoke(table, "ApplyCarriedSync", owner, movedOut, true);

		var cloneData = CloneData(table);
		Assert.Equal(2, cloneData[owner].Items.Count);
		var bottle = cloneData[owner].Items.Single(i => i.InstanceId == 202);
		Assert.Equal(0, bottle.SlotIndex);
		var bag = cloneData[owner].Items.Single(i => i.InstanceId == 101);
		Assert.Empty(bag.Contents);
	}

	[Fact]
	public void ApplyCarriedSync_WhenDeepNestedItemMovesToSlot_KeepsAncestorContainers()
	{
		var table = CreateTable();
		const ulong owner = 2001;
		var knife = new CharacterItemMsg { InstanceId = 303, ItemId = "knife" };
		var pouch = new CharacterItemMsg
		{
			InstanceId = 202,
			ItemId = "pouch",
			Contents = [knife],
		};
		var backpack = new CharacterItemMsg
		{
			InstanceId = 101,
			ItemId = "backpack",
			Contents = [pouch],
		};
		Invoke(table, "ApplySnapshot", owner, new CharacterDataMsg { OwnerSteamId = owner, Items = [backpack] });

		var movedOut = new CharacterItemMsg
		{
			InstanceId = 303,
			ItemId = "knife",
			SlotIndex = 0,
		};
		Invoke(table, "ApplyCarriedSync", owner, movedOut, true);

		var cloneData = CloneData(table);
		Assert.Equal(2, cloneData[owner].Items.Count);
		Assert.Equal(303ul, cloneData[owner].Items.Single(i => i.InstanceId == 303).InstanceId);
		var storedBackpack = cloneData[owner].Items.Single(i => i.InstanceId == 101);
		var storedPouch = Assert.Single(storedBackpack.Contents);
		Assert.Equal(202ul, storedPouch.InstanceId);
		Assert.Empty(storedPouch.Contents);
	}

	[Fact]
	public void ApplyCarriedSync_WhenDeepNestedItemMovesToWorn_KeepsAncestorContainers()
	{
		var table = CreateTable();
		const ulong owner = 2001;
		var knife = new CharacterItemMsg { InstanceId = 303, ItemId = "knife" };
		var pouch = new CharacterItemMsg
		{
			InstanceId = 202,
			ItemId = "pouch",
			Contents = [knife],
		};
		Invoke(table, "ApplySnapshot", owner, new CharacterDataMsg
		{
			OwnerSteamId = owner,
			Items = [new CharacterItemMsg { InstanceId = 101, ItemId = "backpack", Contents = [pouch] }],
		});

		var worn = new CharacterItemMsg
		{
			InstanceId = 303,
			ItemId = "knife",
			SlotIndex = -2,
		};
		Invoke(table, "ApplyCarriedSync", owner, worn, true);

		var cloneData = CloneData(table);
		var storedWorn = cloneData[owner].Items.Single(i => i.InstanceId == 303);
		Assert.Equal(-2, storedWorn.SlotIndex);
		var storedBackpack = cloneData[owner].Items.Single(i => i.InstanceId == 101);
		Assert.Equal(202ul, Assert.Single(storedBackpack.Contents).InstanceId);
	}

	private static object CreateTable() => Activator.CreateInstance(
		CloneFactTable,
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
		null,
		[NullLogger.Instance],
		null)!;

	private static void Invoke(object table, string name, params object[] args)
	{
		var method = CloneFactTable.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"method {name} not found on CloneFactTable");
		method.Invoke(table, args);
	}

	private static IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData(object table)
	{
		var property = CloneFactTable.GetProperty("CloneData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("CloneData property not found on CloneFactTable");
		return (IReadOnlyDictionary<ulong, CharacterDataMsg>)property.GetValue(table)!;
	}
}
