using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression tests for the remote-clone display/domain boundary: container
/// contents rendered on a remote clone are display proxies and must never
/// carry authoritative item-domain instance ids. The sanitizer is exercised
/// through the reflection host because the test project does not compile against
/// the GameAdapter assembly.
/// </summary>
public class CloneInventoryContentSanitizerTests
{
	private static readonly Type Sanitizer = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneInventoryContentSanitizer",
		throwOnError: true)!;

	private static object InvokeWithoutInstanceIds(List<CharacterItemMsg> contents)
	{
		var method = Sanitizer.GetMethod("WithoutInstanceIds",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
			binder: null,
			types: [typeof(IReadOnlyList<CharacterItemMsg>)],
			modifiers: null)
			?? throw new InvalidOperationException("CloneInventoryContentSanitizer.WithoutInstanceIds(IReadOnlyList<CharacterItemMsg>) not found.");
		return method.Invoke(null, [contents])!;
	}

	[Fact]
	public void Sanitizer_StripsInstanceIdsAtEveryNestedLevel()
	{
		var contents = new List<CharacterItemMsg>
		{
			new()
			{
				InstanceId = 100,
				ItemId = "bag",
				Condition = 0.5f,
				SlotIndex = 2,
				Contents =
				[
					new CharacterItemMsg
					{
						InstanceId = 101,
						ItemId = "dogfood",
						Contents =
						[
							new CharacterItemMsg { InstanceId = 102, ItemId = "lid" },
						],
					},
				],
			},
		};

		var result = Assert.IsType<List<CharacterItemMsg>>(InvokeWithoutInstanceIds(contents));

		var bag = Assert.Single(result);
		Assert.Equal(0ul, bag.InstanceId);
		Assert.Equal("bag", bag.ItemId);
		Assert.Equal(0.5f, bag.Condition);
		Assert.Equal(2, bag.SlotIndex);

		var dogFood = Assert.Single(bag.Contents);
		Assert.Equal(0ul, dogFood.InstanceId);
		Assert.Equal("dogfood", dogFood.ItemId);
		Assert.Equal(0ul, Assert.Single(dogFood.Contents).InstanceId);
	}

	[Fact]
	public void Sanitizer_DoesNotMutateSourceData()
	{
		var source = new CharacterItemMsg
		{
			InstanceId = 7,
			ItemId = "bottle",
			Contents = [new CharacterItemMsg { InstanceId = 8, ItemId = "water" }],
		};

		_ = InvokeWithoutInstanceIds([source]);

		Assert.Equal(7ul, source.InstanceId);
		Assert.Equal(8ul, Assert.Single(source.Contents).InstanceId);
	}
}
