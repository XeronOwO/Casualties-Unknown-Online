using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status content payload contract: a mod can serialize a
/// <see cref="ModStatusDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without a private format.
/// </summary>
public class ModStatusDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModStatusDefinition
		{
			DisplayName = "Lead Poisoning",
			Description = "Slowly accumulating heavy-metal exposure.",
			Scope = ModStatusScope.Limb,
			SaveEnabled = false,
			MoodleId = "moodle.lead",
			ShowPerLimbMoodles = true,
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.lead.left" },
				new ModLimbMoodleBinding { LimbName = "RightArm", MoodleId = "moodle.lead.right" }
			],
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModStatusDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(ModStatusScope.Limb, restored.Scope);
		Assert.False(restored.SaveEnabled);
		Assert.Equal("moodle.lead", restored.MoodleId);
		Assert.True(restored.ShowPerLimbMoodles);
		Assert.Equal(2, restored.LimbMoodles.Count);
		Assert.Equal("LeftArm", restored.LimbMoodles[0].LimbName);
		Assert.Equal("moodle.lead.left", restored.LimbMoodles[0].MoodleId);
		Assert.Equal("RightArm", restored.LimbMoodles[1].LimbName);
		Assert.Equal("moodle.lead.right", restored.LimbMoodles[1].MoodleId);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesOptionalDefaults()
	{
		var original = new ModStatusDefinition();

		var restored = ModStatusDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(ModStatusScope.Body, restored!.Scope);
		Assert.True(restored.SaveEnabled);
		Assert.Empty(restored.MoodleId);
		Assert.False(restored.ShowPerLimbMoodles);
		Assert.Empty(restored.LimbMoodles);
		Assert.Empty(restored.CustomData);
	}

	[Fact]
	public void ResolveMoodleId_UsesLimbBinding_WhenPerLimbEnabled()
	{
		var status = new ModStatusDefinition
		{
			Scope = ModStatusScope.Limb,
			MoodleId = "moodle.default",
			ShowPerLimbMoodles = true,
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" }
			]
		};

		Assert.Equal("moodle.left", status.ResolveMoodleId("LeftArm"));
		Assert.Equal("moodle.left", status.ResolveMoodleId("leftarm"));
		Assert.Equal("moodle.default", status.ResolveMoodleId("RightArm"));
		Assert.Equal("moodle.default", status.ResolveMoodleId(null));
	}

	[Fact]
	public void ResolveMoodleId_IgnoresLimbBindings_WhenPerLimbDisabledOrBodyScoped()
	{
		var bodyStatus = new ModStatusDefinition
		{
			Scope = ModStatusScope.Body,
			MoodleId = "moodle.body",
			ShowPerLimbMoodles = true,
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" }
			]
		};

		var limbStatus = new ModStatusDefinition
		{
			Scope = ModStatusScope.Limb,
			MoodleId = "moodle.default",
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" }
			]
		};

		Assert.Equal("moodle.body", bodyStatus.ResolveMoodleId("LeftArm"));
		Assert.Equal("moodle.default", limbStatus.ResolveMoodleId("LeftArm"));
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModStatusDefinition.FromPayload([]));
		Assert.Null(ModStatusDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModStatusDefinition.FromPayload(null!));
	}
}
