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
		Assert.Empty(restored.CustomData);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModStatusDefinition.FromPayload([]));
		Assert.Null(ModStatusDefinition.FromPayload([1, 2, 3]));
		Assert.Null(ModStatusDefinition.FromPayload(null!));
	}
}
