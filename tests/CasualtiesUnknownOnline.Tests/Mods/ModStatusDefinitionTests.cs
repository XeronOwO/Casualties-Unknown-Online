using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Status"/>), the collection
/// members where null means "none", the body/limb defaults the definition
/// declares, and the per-limb moodle routing that needs no payload. Nothing
/// serializes a definition any more, so this suite pins the answers the
/// definition itself owns.
/// </summary>
public class ModStatusDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModStatusDefinition();

		Assert.Equal(ModContentKind.Status, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModStatusDefinition { Id = "test.lead", SchemaVersion = 3 };

		Assert.Equal("test.lead", authored.Id);
		Assert.Equal(3, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModStatusDefinition
		{
			CustomData = null!,
			LimbMoodles = null!
		};

		Assert.Empty(definition.CustomData);
		Assert.Empty(definition.LimbMoodles);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModStatusDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Equal(ModStatusScope.Body, definition.Scope);
		Assert.True(definition.SaveEnabled);
		Assert.Empty(definition.MoodleId);
	}

	[Fact]
	public void LimbBindingDefaults_HoldOnAFreshBinding()
	{
		var binding = new ModLimbMoodleBinding();

		Assert.Empty(binding.LimbName);
		Assert.Empty(binding.MoodleId);
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
}
