using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the local player's own favourite marks (user ruling
/// 2026-10-11 — the mark belongs to the player who made it, never to the item's
/// owner, and it survives a save because it rides that player's character record).
/// The ledger holds no scene, so every rule that is not a Unity write is locked
/// here: the toggle, the guard that keeps a stale id from lighting up on its heir,
/// the spend that turns a mark into the item's own flag, and the replace-not-merge
/// seed that keeps a mark inside the world it was made in.
/// </summary>
public class LocalFavouriteMarksTests
{
	private const ulong Owner = 7101;
	private const ulong OtherOwner = 7103;
	private const ulong Item = 1181116006400;
	private const ulong OtherItem = 1181116006401;
	private const string Scrap = "scrapmetal";

	[Fact]
	public void AMarkIsSetAndClearedByTheSameGesture()
	{
		var marks = new LocalFavouriteMarks();

		Assert.True(marks.Toggle(Item, Owner, Scrap), "the first press marks the item.");
		Assert.True(marks.IsMarked(Item, Owner, Scrap, out var stale));
		Assert.Null(stale);
		Assert.Equal(1, marks.Count);

		Assert.False(marks.Toggle(Item, Owner, Scrap), "the second press clears it.");
		Assert.False(marks.IsMarked(Item, Owner, Scrap, out _));
		Assert.Equal(0, marks.Count);
	}

	[Fact]
	public void AnItemWithoutAnInstanceId_CannotBeMarked()
	{
		var marks = new LocalFavouriteMarks();

		Assert.False(marks.Toggle(0, Owner, Scrap));
		Assert.False(marks.IsMarked(0, Owner, Scrap, out _));
		Assert.Equal(0, marks.Count);
	}

	[Fact]
	public void AMarkIsIndependentOfWhatTheOwnerThinksOfTheItem()
	{
		// The ruling's row 5: the owner's own favourite state is the owner's business.
		// The ledger is asked about the local player's mark only, so an unmarked item
		// answers "no mark" whatever the owner set on it — there is no input here the
		// owner's value could arrive through, and that is the design.
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);
		marks.Toggle(OtherItem, Owner, Scrap);

		Assert.True(marks.IsMarked(Item, Owner, Scrap, out _));
		Assert.True(marks.IsMarked(OtherItem, Owner, Scrap, out _));
		Assert.False(marks.IsMarked(Item + 2, Owner, Scrap, out _), "an item this player never marked carries no mark.");
	}

	[Fact]
	public void AMarkWhoseIdNowNamesAnotherItem_IsDroppedInsteadOfApplied()
	{
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		Assert.False(marks.IsMarked(Item, OtherOwner, Scrap, out var otherOwner),
			"an id that now belongs to another player is not the marked item.");
		Assert.NotNull(otherOwner);
		Assert.Equal(0, marks.Count);

		marks.Toggle(Item, Owner, Scrap);
		Assert.False(marks.IsMarked(Item, Owner, "climbingrope", out var otherDefinition),
			"an id that now carries another definition is not the marked item.");
		Assert.NotNull(otherDefinition);
		Assert.Equal(0, marks.Count);
	}

	[Fact]
	public void AMarkIsSpentWhenTheItemBecomesTheLocalPlayersOwn()
	{
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		Assert.True(marks.TrySpend(Item, Scrap), "the item this player now owns is the one that was marked.");
		Assert.Equal(0, marks.Count);
		Assert.False(marks.IsMarked(Item, Owner, Scrap, out _), "no double state: the mark is gone once the item carries it.");
	}

	[Fact]
	public void AnUnmarkedItem_IsNeverSpent()
	{
		var marks = new LocalFavouriteMarks();

		Assert.False(marks.TrySpend(Item, Scrap));
	}

	[Fact]
	public void AnIdThatNowCarriesAnotherItem_IsNotAdopted()
	{
		// Requirement 3 is about THE marked item. An id whose definition moved on is
		// somebody else's item, and adopting it would set this player's own flag on it.
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		Assert.False(marks.TrySpend(Item, "bandage"));
		Assert.Equal(1, marks.Count);
	}

	[Fact]
	public void SnapshotCopiesTheMarksAndNeverHandsTheLiveSetOut()
	{
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		var snapshot = marks.Snapshot();
		Assert.Single(snapshot);
		Assert.Equal(Item, snapshot[0].ItemInstanceId);
		Assert.Equal(Owner, snapshot[0].OwnerSteamId);
		Assert.Equal(Scrap, snapshot[0].ItemId);

		// The record is what a capture keeps reporting; the ledger is what a gesture
		// changes. Mutating the copy must not reach back into the live set.
		snapshot[0].ItemId = "bandage";
		Assert.True(marks.IsMarked(Item, Owner, Scrap, out _));
	}

	[Fact]
	public void SeedReplacesTheWholeSet()
	{
		// The record is the whole truth for ONE world: a mark it does not carry belongs
		// to a world this client has left, and keeping it is exactly the stale key the
		// ruling forbids applying.
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		marks.Seed([new CharacterFavouriteMarkMsg { OwnerSteamId = Owner, ItemInstanceId = OtherItem, ItemId = "bandage" }]);

		Assert.Equal(1, marks.Count);
		Assert.True(marks.IsMarked(OtherItem, Owner, "bandage", out _));
		Assert.False(marks.IsMarked(Item, Owner, Scrap, out _));
	}

	[Fact]
	public void SeedOfARecordWithoutMarks_ClearsTheSet()
	{
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		marks.Seed(null);
		Assert.Equal(0, marks.Count);

		marks.Toggle(Item, Owner, Scrap);
		marks.Seed([]);
		Assert.Equal(0, marks.Count);
	}

	[Fact]
	public void SeedIgnoresAnEntryWithoutAnInstanceId()
	{
		// An id-less row names no item, so it can never be resolved — the same rule the
		// gesture applies. It must not sit in the set pretending to be a mark.
		var marks = new LocalFavouriteMarks();

		marks.Seed([new CharacterFavouriteMarkMsg { OwnerSteamId = Owner, ItemInstanceId = 0, ItemId = Scrap }]);

		Assert.Equal(0, marks.Count);
	}

	[Fact]
	public void ResetDropsEveryMark()
	{
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);
		marks.Toggle(OtherItem, Owner, Scrap);

		marks.Reset();

		Assert.Equal(0, marks.Count);
		Assert.False(marks.IsMarked(Item, Owner, Scrap, out _));
	}

	[Fact]
	public void TheRoundTripOfAMarkThroughItsWireShapeKeepsEveryOperand()
	{
		// The marks ride the character record, so the wire round trip IS the
		// persistence path: a lost operand would either lose the mark or make it match
		// the wrong item.
		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);

		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(
			NetPacket.Encode(NetMsg.CharacterData, new CharacterDataMsg { FavouriteMarks = marks.Snapshot() }));

		var restored = new LocalFavouriteMarks();
		restored.Seed(decoded.FavouriteMarks);

		Assert.True(restored.IsMarked(Item, Owner, Scrap, out _));
		Assert.Equal(1, restored.Count);
	}

	[Fact]
	public void ACharacterRecordWithoutTheMarksField_SeedsNothing()
	{
		// An older sender: the list decodes to null, which reads as "this character
		// brought no marks" rather than as a set to be guessed.
		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(
			NetPacket.Encode(NetMsg.CharacterData, new CharacterDataMsg()));

		Assert.Null(decoded.FavouriteMarks);

		var marks = new LocalFavouriteMarks();
		marks.Toggle(Item, Owner, Scrap);
		marks.Seed(decoded.FavouriteMarks);

		Assert.Equal(0, marks.Count);
	}
}
