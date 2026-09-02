using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The typed status wire payload contract: a host committed status becomes a
/// versioned <see cref="ModStatusUpdate"/> byte frame that a guest can decode
/// and apply without a private/JObject format.
/// </summary>
public class ModStatusUpdateTests
{
	[Fact]
	public void BodySetRoundTrip_PreservesKeyScopeSchemaAndValue()
	{
		var original = ModStatusUpdate.ForBody("bleeding", 2001, 3, [1, 2, 3]);

		var restored = ModStatusUpdate.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal("bleeding", restored!.StatusId);
		Assert.Equal(ModStatusScope.Body, restored.Scope);
		Assert.Equal(2001UL, restored.PlayerSteamId);
		Assert.Equal(-1, restored.LimbSlot);
		Assert.Equal(3, restored.SchemaVersion);
		Assert.Equal([1, 2, 3], restored.Value);
		Assert.False(restored.Remove);
	}

	[Fact]
	public void LimbRemoveRoundTrip_PreservesLimbSlotAndRemoveFlag()
	{
		var original = ModStatusUpdate.RemoveLimb("limb.bleed", 2001, 2, 4);

		var restored = ModStatusUpdate.FromPayload(original.ToPayload());

		Assert.NotNull(restored);
		Assert.Equal("limb.bleed", restored!.StatusId);
		Assert.Equal(ModStatusScope.Limb, restored.Scope);
		Assert.Equal(2, restored.LimbSlot);
		Assert.Equal(4, restored.SchemaVersion);
		Assert.Empty(restored.Value);
		Assert.True(restored.Remove);
	}

	[Fact]
	public void InvalidPayload_ReturnsNull()
	{
		Assert.Null(ModStatusUpdate.FromPayload([]));
		Assert.Null(ModStatusUpdate.FromPayload([1, 2, 3]));
		Assert.Null(ModStatusUpdate.FromPayload(null!));
	}
}
