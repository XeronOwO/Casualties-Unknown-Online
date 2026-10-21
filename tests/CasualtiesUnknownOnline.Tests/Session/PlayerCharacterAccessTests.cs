using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The clone the direct player-interaction services build before they SAVE a
/// character snapshot. It is a field-by-field copy, so every field it forgets is
/// dropped from the store the cut, the reconnect restore and the continue all read
/// — the native character fields included (S3.4b): a heal, a transfer or a remote
/// application would otherwise silently degrade the player's stored character.
/// </summary>
public sealed class PlayerCharacterAccessTests
{
	[Fact]
	public void CloneCharacter_CarriesTheNativeCharacterFields()
	{
		var source = new CharacterDataMsg
		{
			Items = [new CharacterItemMsg { InstanceId = 7, ItemId = "bandage", SlotIndex = 1, Condition = 0.5f }],
			NativeFields = new CharacterNativeFieldsMsg
			{
				LastHappiness = [0.1f, 0.2f],
				CaloriesConsumed = 4100,
				CharacterInfo = [171, 24, 8123, 3],
			},
		};

		var clone = PlayerCharacterAccess.CloneCharacter(source);

		var fields = Assert.IsType<CharacterNativeFieldsMsg>(clone.NativeFields);
		Assert.Equal([0.1f, 0.2f], fields.LastHappiness);
		Assert.Equal(4100, fields.CaloriesConsumed);
		Assert.Equal([171, 24, 8123, 3], fields.CharacterInfo);
	}

	[Fact]
	public void CloneCharacter_CarriesTheLocalFavouriteMarks()
	{
		// Same hazard as the native fields above, and the same consequence: these
		// interaction services SAVE the clone over the stored character, so a dropped
		// list would silently unmark every item this player starred in somebody else's
		// backpack — while they are still playing in that world.
		var source = new CharacterDataMsg
		{
			FavouriteMarks =
			[
				new CharacterFavouriteMarkMsg { OwnerSteamId = 9002, ItemInstanceId = 41, ItemId = "scrapmetal" },
			],
		};

		var clone = PlayerCharacterAccess.CloneCharacter(source);

		var mark = Assert.Single(Assert.IsType<List<CharacterFavouriteMarkMsg>>(clone.FavouriteMarks));
		Assert.Equal(9002UL, mark.OwnerSteamId);
		Assert.Equal(41UL, mark.ItemInstanceId);
		Assert.Equal("scrapmetal", mark.ItemId);
	}

	[Fact]
	public void CloneCharacter_CarriesTheLayerStampAndTheRespawnIdentity()
	{
		// Same hazard as the fields above: these interaction services SAVE the clone over the stored
		// character. A dropped layer stamp makes the next restore apply a position from a layer the
		// player is no longer in; a dropped respawn flag turns the next-level revive back into an
		// ordinary handback a descent-preserved body refuses.
		var source = new CharacterDataMsg
		{
			Generation = new WorldGenerationMsg { RunEpoch = 5, LayerIndex = 3 },
			Respawn = true,
		};

		var clone = PlayerCharacterAccess.CloneCharacter(source);

		Assert.Same(source.Generation, clone.Generation);
		Assert.Equal(3, clone.Generation!.LayerIndex);
		Assert.True(clone.Respawn);
	}

	[Fact]
	public void CloneCharacter_IsAnIndependentCopyOfTheMutableTables()
	{
		var source = new CharacterDataMsg
		{
			Items = [new CharacterItemMsg { InstanceId = 7, ItemId = "bandage", SlotIndex = 1 }],
			NativeFields = new CharacterNativeFieldsMsg { LastHappiness = [0.1f], CaloriesConsumed = 1, CharacterInfo = [1, 2, 3, 4] },
		};

		var clone = PlayerCharacterAccess.CloneCharacter(source);
		clone.Items[0].Condition = 0.9f;
		clone.Items.Add(new CharacterItemMsg { InstanceId = 8, ItemId = "rope", SlotIndex = 2 });

		// The clone is what gets stored, so mutating its tables must not reach the
		// snapshot the live body reported (the store then holds the live truth until the
		// next report). The native fields are deliberately shared: they are a captured
		// value the restore only reads, and the wire/server shape never mutates them in
		// place.
		Assert.Equal(0f, source.Items[0].Condition);
		Assert.Single(source.Items);
		Assert.Same(source.NativeFields, clone.NativeFields);
	}
}
