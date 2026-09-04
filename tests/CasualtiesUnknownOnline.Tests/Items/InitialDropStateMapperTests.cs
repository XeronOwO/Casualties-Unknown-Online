using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The full initial drop state (fresh flag, velocity, rotation and angular
/// velocity) must survive the entity/building destruction relay exactly like
/// the block-break relay already does. This pure mapper is the single source
/// for both block and trap/building drop entries so the two families cannot
/// drift again.
/// </summary>
public class InitialDropStateMapperTests
{
	[Fact]
	public void TrapDrop_PreservesFullInitialState()
	{
		var drop = new TrapDropEntryMsg
		{
			ItemId = 42,
			Item = new CharacterItemMsg { ItemId = "dog_food" },
			Position = new NetVector2Msg(1f, 2f),
			Velocity = new NetVector2Msg(3f, 4f),
			Rotation = 45f,
			FreshItemDrop = true,
			AngularVelocity = 12.5f,
		};

		var world = InitialDropStateMapper.ToWorldItem(drop);

		Assert.Equal(42ul, world.ItemId);
		Assert.Equal("dog_food", world.Item.ItemId);
		Assert.Equal(1f, world.Pos.X);
		Assert.Equal(2f, world.Pos.Y);
		Assert.Equal(3f, world.Vel.X);
		Assert.Equal(4f, world.Vel.Y);
		Assert.Equal(45f, world.Rotation);
		Assert.True(world.FreshItemDrop);
		Assert.Equal(12.5f, world.AngularVelocity);
	}

	[Fact]
	public void BlockDrop_PreservesFullInitialState()
	{
		var drop = new BlockDropEntryMsg
		{
			ItemId = 7,
			Item = new CharacterItemMsg { ItemId = "stone" },
			Position = new NetVector2Msg(-1f, -2f),
			Velocity = new NetVector2Msg(0.5f, -0.75f),
			Rotation = 180f,
			FreshItemDrop = true,
			AngularVelocity = -3f,
		};

		var world = InitialDropStateMapper.ToWorldItem(drop);

		Assert.Equal(7ul, world.ItemId);
		Assert.Equal("stone", world.Item.ItemId);
		Assert.Equal(-1f, world.Pos.X);
		Assert.Equal(-2f, world.Pos.Y);
		Assert.Equal(0.5f, world.Vel.X);
		Assert.Equal(-0.75f, world.Vel.Y);
		Assert.Equal(180f, world.Rotation);
		Assert.True(world.FreshItemDrop);
		Assert.Equal(-3f, world.AngularVelocity);
	}
}
