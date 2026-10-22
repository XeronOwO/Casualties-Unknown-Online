using System.Text.Json;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.CharacterData;

/// <summary>
/// The one deep copy of a character snapshot. Two owners hold a snapshot at once — the host's
/// saved-character table (the carrier every cut writes <c>characters/&lt;key&gt;.json</c> from) and
/// the clone presentation's fact table, which MUTATES the snapshot it renders when a carried-sync
/// moves an item in or out of the clone. The copy must therefore be complete (a dropped field
/// silently degrades the next cut, reconnect and continue) and deep in the item tree (the mutation
/// target). The JSON comparison below is the completeness check: it fails on any public field the
/// copy forgets; ownership is pinned separately — deep for items/limbs, SHARED for Health/Skills by
/// the contract <see cref="CharacterDataClone"/> states.
/// </summary>
public class CharacterDataCloneTests
{
	[Fact]
	public void TheCopyCarriesEveryFieldAndIsADifferentObject()
	{
		var source = FullyPopulated();

		var clone = CharacterDataClone.Of(source);

		Assert.NotSame(source, clone);
		Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(clone));

		// The SHARED-LEAF contract: Health and Skills are written through by the host's vitals model
		// and the clone presentation by design, so the copy shares them; the item and limb trees are
		// the deep half (pinned below).
		Assert.Same(source.Health, clone.Health);
		Assert.Same(source.Skills, clone.Skills);
	}

	[Fact]
	public void TheCopyOwnsItsItemTree()
	{
		var source = FullyPopulated();
		var clone = CharacterDataClone.Of(source);

		Assert.NotSame(source.Items, clone.Items);
		Assert.NotSame(source.Items[0], clone.Items[0]);
		Assert.NotSame(source.Items[0].Contents, clone.Items[0].Contents);
		Assert.NotSame(source.Items[0].Contents[0], clone.Items[0].Contents[0]);
		Assert.NotSame(source.Items[0].Liquids, clone.Items[0].Liquids);

		// Components stay SHARED by the copy's contract: the report path's presentation only reads
		// them, and the interaction services that replace a component are authority writes that mean
		// to change the stored character.

		// The presentation's carried-sync removal is exactly this mutation: it must not reach the
		// stored character the cut writes.
		source.Items[0].Contents.Clear();
		source.Items[0].SlotIndex = 4;
		Assert.Single(clone.Items[0].Contents);
		Assert.Equal(2, clone.Items[0].SlotIndex);
	}

	[Fact]
	public void TheCopyOwnsItsLimbsAndLimbComponents()
	{
		var source = FullyPopulated();
		var clone = CharacterDataClone.Of(source);

		Assert.NotSame(source.Limbs, clone.Limbs);
		Assert.NotSame(source.Limbs[0], clone.Limbs[0]);
		Assert.NotSame(source.Limbs[0].Components, clone.Limbs[0].Components);

		source.Limbs[0].BleedAmount = 99f;
		source.Limbs[0].Components.Clear();
		Assert.NotEqual(99f, clone.Limbs[0].BleedAmount);
		Assert.Single(clone.Limbs[0].Components);
	}

	private static CharacterDataMsg FullyPopulated() => new()
	{
		Skills = new CharacterSkillsMsg { Strength = 9, Resistance = 8, Intelligence = 10, ExpStrength = 1.5f, ExpResistance = 2.5f, ExpIntelligence = 3.5f },
		Health = new CharacterHealthMsg { BloodVolume = 87.5f, Hunger = 40f, Thirst = 30f, TotalBleedSpeed = 0.25f, HeadMouth = HeadMouthState.Open, LegSpeedMult = 0.6f },
		Limbs =
		[
			new CharacterLimbMsg
			{
				Index = 2,
				SkinHealth = 70f,
				MuscleHealth = 55f,
				Broken = true,
				Dislocated = false,
				Splinted = true,
				Infected = true,
				InfectionAmount = 0.4f,
				BleedAmount = 1.25f,
				DisinfectionTime = 3f,
				Pain = 12f,
				DislocationTimer = 1f,
				BoneHealTimer = 2f,
				BlockedBleeding = true,
				Shrapnel = 3,
				FurBloodAmount = 0.1f,
				BandageSlowAmount = 0.2f,
				SkinHealAmount = 0.3f,
				Dismembered = false,
				Components = [new ComponentStateMsg { TypeName = "SplintLimb" }],
				IsHead = false,
				IsVital = true,
				ConnectedLimbIndices = [1, 3],
				DistanceToHeart = 1,
			},
		],
		Items =
		[
			new CharacterItemMsg
			{
				InstanceId = 100UL,
				ItemId = "bag",
				Condition = 0.75f,
				SlotIndex = 2,
				Favourited = true,
				Components =
				[
					new ComponentStateMsg
					{
						TypeName = "CustomItemBehaviour",
						Fields =
						[
							new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 7, StringValue = "x", StringList = ["a"] },
						],
					},
				],
				Contents =
				[
					new CharacterItemMsg { InstanceId = 101UL, ItemId = "rope", SlotIndex = 2, Condition = 0.5f },
				],
				Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 0.5f }],
			},
		],
		HandSlot = 1,
		OwnerSteamId = 76561198000000000UL,
		Position = new NetVector2Msg(1.5f, 2.5f),
		SlotCount = 6,
		NativeFields = new CharacterNativeFieldsMsg
		{
			LastHappiness = [1f, 2f, 3f],
			CaloriesConsumed = 1234,
			CharacterInfo = [180, 33, 7, 2],
		},
		FavouriteMarks = [new CharacterFavouriteMarkMsg { OwnerSteamId = 2002UL, ItemInstanceId = 100UL, ItemId = "bag" }],
		Generation = new WorldGenerationMsg { RunEpoch = 3UL, LayerIndex = 2 },
		Respawn = true,
	};
}
