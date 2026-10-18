using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The finiteness guard on a report: every physiological value the host will map onto a body is
/// validated, and the presentation-only item state is deliberately out of scope so one corrupt
/// item can never freeze a player's whole physiological report.
/// </summary>
public class CharacterDataFiniteTests
{
	[Fact]
	public void AFiniteReport_IsAccepted() => Assert.Null(CharacterDataFinite.FirstNonFinite(Snapshot()));

	[Fact]
	public void ANonFiniteHealthField_IsNamed()
	{
		var data = Snapshot();
		data.Health!.BloodViscosity = float.NaN;

		Assert.Equal("health.BloodViscosity", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteOwnedField_IsNamed()
	{
		var data = Snapshot();
		data.Health!.TotalBleedSpeed = float.PositiveInfinity;

		Assert.Equal("health.TotalBleedSpeed", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteLimbField_IsNamedWithTheLimbIndex()
	{
		var data = Snapshot();
		data.Limbs.Add(new CharacterLimbMsg { Index = 2, BleedAmount = float.NaN, SkinHealth = 50f });

		Assert.Equal("limb 2.BleedAmount", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteSkill_IsNamed()
	{
		var data = Snapshot();
		data.Skills = new CharacterSkillsMsg { ExpStrength = float.NaN };

		Assert.Equal("skills.ExpStrength", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteItemCondition_IsRefused_BecauseItReachesTheDerivedWeight()
	{
		var data = Snapshot();
		data.Items.Add(new CharacterItemMsg { ItemId = "bandage", Condition = float.NaN });

		Assert.Equal("item bandage.Condition", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteLiquidAmount_IsRefused()
	{
		var data = Snapshot();
		data.Items.Add(new CharacterItemMsg
		{
			ItemId = "bottle",
			Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = float.PositiveInfinity }],
		});

		Assert.Equal("item bottle.Liquid water", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFiniteNestedItem_IsRefusedThroughItsParent()
	{
		var data = Snapshot();
		var parent = new CharacterItemMsg { ItemId = "backpack" };
		parent.Contents.Add(new CharacterItemMsg { ItemId = "bandage", Condition = float.NaN });
		data.Items.Add(parent);

		Assert.Equal("item backpack/bandage.Condition", CharacterDataFinite.FirstNonFinite(data));
	}

	[Fact]
	public void ANonFloatComponentValue_DoesNotRefuseTheReport()
	{
		var data = Snapshot();
		data.Items.Add(new CharacterItemMsg
		{
			ItemId = "radio",
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = "Radio",
					Fields = [new ComponentFieldMsg { Name = "station", Kind = 4, FloatValue = float.NaN, StringValue = "fm" }],
				},
			],
		});

		Assert.Null(CharacterDataFinite.FirstNonFinite(data));
	}

	private static CharacterDataMsg Snapshot() => new()
	{
		Health = new CharacterHealthMsg
		{
			BloodVolume = 100f,
			TotalBleedSpeed = 0.1f,
			BloodViscosity = 0f,
			Temperature = 37f,
		},
		Skills = new CharacterSkillsMsg(),
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = 1f, SkinHealth = 100f }],
	};
}
