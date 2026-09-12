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
