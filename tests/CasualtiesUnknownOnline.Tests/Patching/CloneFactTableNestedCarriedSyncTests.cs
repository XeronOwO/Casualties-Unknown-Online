using System;
using System.Collections.Generic;
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
