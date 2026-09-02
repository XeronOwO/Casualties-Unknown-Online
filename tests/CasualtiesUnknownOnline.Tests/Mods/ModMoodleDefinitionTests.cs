using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed moodle content payload contract: a mod can serialize a
/// <see cref="ModMoodleDefinition"/> into the opaque byte payload and the
/// Runtime/Game Adapter can read it back without exposing Unity types.
/// </summary>
public class ModMoodleDefinitionTests
{
	[Fact]
	public void RoundTrip_PreservesCoreFields()
	{
		var original = new ModMoodleDefinition
		{
			DisplayName = "Lead Poisoning",
			Description = "You're feeling woozy.",
			Intensity = 2,
			IconId = "icons.lead",
			Critical = true,
			ChippedOnly = true,
			Important = false,
			HoldSeconds = 1.5f,
			CustomData = new Dictionary<string, string>
			{
				["mod.metadata"] = "kept"
			}
		};

		var restored = ModMoodleDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal(original.DisplayName, restored!.DisplayName);
		Assert.Equal(original.Description, restored.Description);
		Assert.Equal(2, restored.Intensity);
		Assert.Equal("icons.lead", restored.IconId);
		Assert.True(restored.Critical);
		Assert.True(restored.ChippedOnly);
		Assert.False(restored.Important);
		Assert.Equal(1.5f, restored.HoldSeconds);
		Assert.Equal("kept", restored.CustomData["mod.metadata"]);
	}

	[Fact]
	public void RoundTrip_PreservesDefaults()
	{
		var original = new ModMoodleDefinition();

		var restored = ModMoodleDefinition.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.True(restored!.Important);
		Assert.Equal(0.75f, restored.HoldSeconds);
		Assert.Empty(restored.IconId);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModMoodleDefinition.FromPayload([]));
		Assert.Null(ModMoodleDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModMoodleDefinition.FromPayload(null!));
	}
}
