using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed moodle definition a mod hands to <see cref="IModContent"/>: the
/// identity its type fixes (<see cref="ModContentKind.Moodle"/>), the collection
/// members where null means "none", the presentation defaults the definition and
/// its icon animation declare, and the limb-text formatting that needs no
/// payload. Nothing serializes a definition any more, so this suite pins the
/// answers the definition itself owns.
/// </summary>
public class ModMoodleDefinitionTests
{
	[Fact]
	public void Identity_IsFixedByTheType()
	{
		var defaults = new ModMoodleDefinition();

		Assert.Equal(ModContentKind.Moodle, defaults.Kind);
		Assert.Empty(defaults.Id);
		Assert.Equal(1, defaults.SchemaVersion);

		var authored = new ModMoodleDefinition { Id = "test.lead.moodle", SchemaVersion = 2 };

		Assert.Equal("test.lead.moodle", authored.Id);
		Assert.Equal(2, authored.SchemaVersion);
	}

	[Fact]
	public void NullCollectionMembers_MeanNone()
	{
		var definition = new ModMoodleDefinition
		{
			CustomData = null!,
			IconAnimation = new ModMoodleAnimation { FramePaths = null! }
		};

		Assert.Empty(definition.CustomData);
		Assert.Empty(definition.IconAnimation!.FramePaths);
	}

	[Fact]
	public void DeclaredDefaults_HoldOnAFreshDefinition()
	{
		var definition = new ModMoodleDefinition();

		Assert.Empty(definition.DisplayName);
		Assert.Empty(definition.Description);
		Assert.Equal(1, definition.Intensity);
		Assert.Empty(definition.IconId);
		Assert.True(definition.Important);
		Assert.Equal(0.75f, definition.HoldSeconds);
		Assert.Empty(definition.LimbDisplayNameFormat);
		Assert.Empty(definition.LimbDescriptionFormat);
	}

	[Fact]
	public void IconAnimationDefaults_HoldOnAFreshAnimation()
	{
		var animation = new ModMoodleAnimation();

		Assert.Equal(12f, animation.FramesPerSecond);
		Assert.True(animation.Loop);
	}

	[Fact]
	public void FormatLimbText_UsesAuthoredTemplates_AndFallsBack()
	{
		var moodle = new ModMoodleDefinition
		{
			DisplayName = "Bleeding",
			Description = "You are bleeding.",
			LimbDisplayNameFormat = "{name} ({limb})",
			LimbDescriptionFormat = "{limb}: {description}"
		};

		Assert.Equal("Bleeding (Left Arm)", moodle.FormatLimbDisplayName("Left Arm"));
		Assert.Equal("Left Arm: You are bleeding.", moodle.FormatLimbDescription("Left Arm"));
		Assert.Equal("Bleeding", moodle.FormatLimbDisplayName(""));
		Assert.Equal("You are bleeding.", moodle.FormatLimbDescription(""));
	}
}
