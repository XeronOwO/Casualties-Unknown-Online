using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public sealed class PlayerColorResolverTests
{
	[Fact]
	public void SameSteamId_AlwaysResolvesToSameColor()
	{
		var first = PlayerColorResolver.Resolve(123456789UL);
		var second = PlayerColorResolver.Resolve(123456789UL);

		Assert.Equal(first.R, second.R);
		Assert.Equal(first.G, second.G);
		Assert.Equal(first.B, second.B);
		Assert.Equal(first.A, second.A);
	}

	[Fact]
	public void ResolvedColors_AreOpaqueAndInValidFloatRange()
	{
		foreach (var steamId in SteamIds())
		{
			var color = PlayerColorResolver.Resolve(steamId);
			Assert.InRange(color.R, 0f, 1f);
			Assert.InRange(color.G, 0f, 1f);
			Assert.InRange(color.B, 0f, 1f);
			Assert.Equal(1f, color.A);
		}
	}

	[Fact]
	public void ResolvedColors_CoverAtLeastFourDistinctPaletteEntries()
	{
		var colors = new HashSet<(float R, float G, float B)>();
		foreach (var steamId in SteamIds())
		{
			var color = PlayerColorResolver.Resolve(steamId);
			colors.Add((color.R, color.G, color.B));
		}

		Assert.True(colors.Count >= 4, $"expected a useful spread of teammate colors, got {colors.Count}");
	}

	private static IEnumerable<ulong> SteamIds()
	{
		for (ulong i = 1; i <= 64; i++)
		{
			yield return i * 0x100000001B3UL; // a few identity-space-like values
		}
	}
}
